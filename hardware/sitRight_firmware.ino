#include <Wire.h>
#include <EEPROM.h>

// ==================== 编译开关 ====================
// 0: 关闭调试文本（运行态仅输出协议数字） 1: 开启调试文本
#define DEBUG_SERIAL 0

#if DEBUG_SERIAL
#define DBG_PRINT(x) Serial.print(x)
#define DBG_PRINTLN(x) Serial.println(x)
#else
#define DBG_PRINT(x)
#define DBG_PRINTLN(x)
#endif

// ==================== 配置常量 ====================
const int MPU_addr = 0x68;              // MPU6050 I2C地址
const unsigned long BAUD_RATE = 115200; // 串口波特率
const int DELAY_MS = 200;               // 主循环延迟(ms)

// 校准配置
const int CALIBRATE_SAMPLES = 10;       // 校准采样次数
const int CALIBRATE_WINDOW_MS = 500;    // 校准采样窗口(ms)
const int SAMPLE_INTERVAL_MS = CALIBRATE_WINDOW_MS / CALIBRATE_SAMPLES; // 采样间隔

// blurLevel 映射参数
const float BLUR_SMOOTH_ALPHA = 0.25;   // 一阶平滑系数

// ==================== EEPROM 存储结构 ====================
struct CalibData {
  uint16_t magic;       // 魔数 0xA5A5
  uint8_t  version;     // 版本号 0x02
  float    normalX, normalY, normalZ;   // 坐正姿态重力向量
  float    slouchX, slouchY, slouchZ;   // 驼背姿态重力向量
  uint8_t  checksum;   // 简单校验和
};

const uint16_t EEPROM_MAGIC = 0xA5A5;
const uint8_t EEPROM_VERSION = 0x02;

// ==================== 全局变量 ====================
// MPU6050 原始数据
int16_t AcX, AcY, AcZ, Tmp, GyX, GyY, GyZ;
// 加速度
float ax, ay, az;

// 校准参数: 重力方向单位向量
float calibNormalX = 0, calibNormalY = 0, calibNormalZ = 0;
float calibSlouchX = 0, calibSlouchY = 0, calibSlouchZ = 0;
bool hasNormal = false;
bool hasSlouch = false;

// blur 输出状态
float blurSmoothed = 0.0;
bool blurSmootherInitialized = false;

// 系统状态
volatile bool isCalibrating = false;  // 校准进行中标志

// 串口接收缓冲区
const int MAX_CMD_LEN = 32;
char cmdBuffer[MAX_CMD_LEN];
int cmdIndex = 0;

// ==================== 函数声明 ====================
void eepromReadCalib();
void eepromWriteCalib();
uint8_t calculateChecksum(const CalibData& data);
bool performCalibration(float &outX, float &outY, float &outZ);
void processCommand(const char* cmd);
void sendAck(const char* cmdType);
void sendErr(const char* errCode);
float clampFloat(float value, float minValue, float maxValue);
int computeBlurLevel(float curX, float curY, float curZ);
int smoothBlurLevel(int blurRaw);

// ==================== EEPROM 操作 ====================

// 计算校验和
uint8_t calculateChecksum(const CalibData& data) {
  uint8_t sum = 0;
  const uint8_t* ptr = (const uint8_t*)&data;
  for (size_t i = 0; i < sizeof(CalibData) - 1; i++) {
    sum ^= ptr[i];
  }
  return sum;
}

// 从EEPROM读取校准数据
void eepromReadCalib() {
  CalibData data;
  EEPROM.get(0, data);

  if (data.magic != EEPROM_MAGIC || data.version != EEPROM_VERSION) {
    hasNormal = false;
    hasSlouch = false;
    return;
  }

  uint8_t storedChecksum = data.checksum;
  data.checksum = 0;
  uint8_t calcChecksum = calculateChecksum(data);

  if (storedChecksum != calcChecksum) {
    hasNormal = false;
    hasSlouch = false;
    return;
  }

  calibNormalX = data.normalX;
  calibNormalY = data.normalY;
  calibNormalZ = data.normalZ;
  hasNormal = true;

  calibSlouchX = data.slouchX;
  calibSlouchY = data.slouchY;
  calibSlouchZ = data.slouchZ;
  hasSlouch = true;
}

// 写入校准数据到EEPROM
void eepromWriteCalib() {
  CalibData data;
  data.magic = EEPROM_MAGIC;
  data.version = EEPROM_VERSION;
  data.normalX = calibNormalX;
  data.normalY = calibNormalY;
  data.normalZ = calibNormalZ;
  data.slouchX = calibSlouchX;
  data.slouchY = calibSlouchY;
  data.slouchZ = calibSlouchZ;
  data.checksum = 0;
  data.checksum = calculateChecksum(data);

  EEPROM.put(0, data);
}

// ==================== 校准执行 ====================

// 执行校准采样：500ms内采样10次，采集归一化重力向量
bool performCalibration(float &outX, float &outY, float &outZ) {
  float sumX = 0, sumY = 0, sumZ = 0;
  int validSamples = 0;

  for (int i = 0; i < CALIBRATE_SAMPLES; i++) {
    Wire.beginTransmission(MPU_addr);
    Wire.write(0x3B);
    Wire.endTransmission(false);
    Wire.requestFrom(MPU_addr, 14, true);

    AcX = Wire.read() << 8 | Wire.read();
    AcY = Wire.read() << 8 | Wire.read();
    AcZ = Wire.read() << 8 | Wire.read();

    float rawX = AcX / 16384.0;
    float rawY = AcY / 16384.0;
    float rawZ = AcZ / 16384.0;

    float mag = sqrt(rawX * rawX + rawY * rawY + rawZ * rawZ);
    if (mag < 0.1) continue;

    sumX += rawX / mag;
    sumY += rawY / mag;
    sumZ += rawZ / mag;
    validSamples++;

    delay(SAMPLE_INTERVAL_MS);
  }

  if (validSamples < CALIBRATE_SAMPLES / 2)
    return false;

  outX = sumX / validSamples;
  outY = sumY / validSamples;
  outZ = sumZ / validSamples;
  float mag = sqrt(outX * outX + outY * outY + outZ * outZ);
  outX /= mag;
  outY /= mag;
  outZ /= mag;

  return true;
}

// ==================== 命令处理 ====================

// 处理串口命令
void processCommand(const char* cmd) {
  if (isCalibrating) {
    sendErr("BUSY");
    return;
  }

  if (strcmp(cmd, "CMD:SET_NORMAL") == 0) {
    isCalibrating = true;
    float nx, ny, nz;
    if (!performCalibration(nx, ny, nz)) {
      sendErr("CALIBRATE_TIMEOUT");
      isCalibrating = false;
      return;
    }

    calibNormalX = nx;
    calibNormalY = ny;
    calibNormalZ = nz;
    hasNormal = true;

    eepromWriteCalib();

    CalibData verify;
    EEPROM.get(0, verify);
    if (verify.magic != EEPROM_MAGIC) {
      hasNormal = false;
      sendErr("EEPROM_WRITE_FAIL");
      isCalibrating = false;
      return;
    }

    sendAck("SET_NORMAL");
    isCalibrating = false;

  } else if (strcmp(cmd, "CMD:SET_SLOUCH") == 0) {
    isCalibrating = true;
    float sx, sy, sz;
    if (!performCalibration(sx, sy, sz)) {
      sendErr("CALIBRATE_TIMEOUT");
      isCalibrating = false;
      return;
    }

    calibSlouchX = sx;
    calibSlouchY = sy;
    calibSlouchZ = sz;
    hasSlouch = true;

    eepromWriteCalib();

    CalibData verify;
    EEPROM.get(0, verify);
    if (verify.magic != EEPROM_MAGIC) {
      hasSlouch = false;
      sendErr("EEPROM_WRITE_FAIL");
      isCalibrating = false;
      return;
    }

    sendAck("SET_SLOUCH");
    isCalibrating = false;

  } else {
    sendErr("UNKNOWN_CMD");
  }
}

// ==================== 协议回包 ====================

// 发送成功回包
void sendAck(const char* cmdType) {
  Serial.print("ACK:");
  Serial.println(cmdType);
}

// 发送错误回包（协议回包，始终输出）
void sendErr(const char* errCode) {
  Serial.print("ERR:");
  Serial.println(errCode);
}

// ==================== 辅助函数 ====================

float clampFloat(float value, float minValue, float maxValue) {
  if (value < minValue) {
    return minValue;
  }
  if (value > maxValue) {
    return maxValue;
  }
  return value;
}

// 将当前重力向量投影到校准轴，映射为 0~100 的非线性 blurLevel
int computeBlurLevel(float curX, float curY, float curZ) {
  float devX = curX - calibNormalX;
  float devY = curY - calibNormalY;
  float devZ = curZ - calibNormalZ;

  float axisX = calibSlouchX - calibNormalX;
  float axisY = calibSlouchY - calibNormalY;
  float axisZ = calibSlouchZ - calibNormalZ;

  float dotDevAxis = devX * axisX + devY * axisY + devZ * axisZ;
  float dotAxisAxis = axisX * axisX + axisY * axisY + axisZ * axisZ;

  if (dotAxisAxis < 0.001) return 0;

  float projection = dotDevAxis / dotAxisAxis;

  float x = clampFloat(projection, 0.0, 1.0);

  float b = 0.0;
  if (x < 0.1) {
    b = 0;
  } else {
    b = 100.0 * pow((x - 0.1) / 0.9, 0.75);
  }

  b = clampFloat(b, 0.0, 100.0);
  return (int)round(b);
}

int smoothBlurLevel(int blurRaw) {
  if (!blurSmootherInitialized) {
    blurSmoothed = (float)blurRaw;
    blurSmootherInitialized = true;
  } else {
    blurSmoothed = blurSmoothed + BLUR_SMOOTH_ALPHA * ((float)blurRaw - blurSmoothed);
  }

  blurSmoothed = clampFloat(blurSmoothed, 0.0, 100.0);
  return (int)round(blurSmoothed);
}

// 去除字符串两端空白
void trimWhitespace(char* str) {
  char* start = str;
  char* end;

  // 去除前导空白
  while (*start == ' ' || *start == '\t' || *start == '\r') {
    start++;
  }

  // 去除尾部空白
  end = start + strlen(start) - 1;
  while (end > start && (*end == ' ' || *end == '\t' || *end == '\r' || *end == '\n')) {
    end--;
  }
  *(end + 1) = '\0';

  // 移动到开头
  if (start != str) {
    memmove(str, start, strlen(start) + 1);
  }
}

// ==================== 主程序 ====================

void setup() {
  Wire.begin();
  Serial.begin(BAUD_RATE);

  Wire.beginTransmission(MPU_addr);
  Wire.write(0x6B);
  Wire.write(0);
  Wire.endTransmission(true);

  eepromReadCalib();

  DBG_PRINTLN("SitRight Firmware v2.0");
  DBG_PRINT("Calibration: ");
  DBG_PRINT(hasNormal ? "normal OK" : "normal MISSING");
  DBG_PRINT(", ");
  DBG_PRINTLN(hasSlouch ? "slouch OK" : "slouch MISSING");
  DBG_PRINTLN("Ready. Commands: CMD:SET_NORMAL, CMD:SET_SLOUCH");
}

void loop() {
  while (Serial.available() > 0) {
    char c = Serial.read();

    if (c == '\n') {
      cmdBuffer[cmdIndex] = '\0';
      if (cmdIndex > 0) {
        trimWhitespace(cmdBuffer);

        if (strlen(cmdBuffer) > 0) {
          processCommand(cmdBuffer);
        }
      }

      cmdIndex = 0;
    } else if (cmdIndex < MAX_CMD_LEN - 1) {
      cmdBuffer[cmdIndex++] = c;
    }
  }

  if (hasNormal && hasSlouch && !isCalibrating) {
    Wire.beginTransmission(MPU_addr);
    Wire.write(0x3B);
    Wire.endTransmission(false);
    Wire.requestFrom(MPU_addr, 14, true);

    AcX = Wire.read() << 8 | Wire.read();
    AcY = Wire.read() << 8 | Wire.read();
    AcZ = Wire.read() << 8 | Wire.read();

    ax = AcX / 16384.0;
    ay = AcY / 16384.0;
    az = AcZ / 16384.0;
    float mag = sqrt(ax * ax + ay * ay + az * az);

    if (mag > 0.1) {
      ax /= mag;
      ay /= mag;
      az /= mag;

      int blurRaw = computeBlurLevel(ax, ay, az);
      int blurStable = smoothBlurLevel(blurRaw);
      Serial.println(blurStable);
    }

    delay(DELAY_MS);
  }
}
