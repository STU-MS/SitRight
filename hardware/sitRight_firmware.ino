#include <Wire.h>
#include <EEPROM.h>

// ==================== 编译开关 ====================
// 运行态默认输出 blurLevel（0~100）
#define OUTPUT_MODE_BLUR
// #define OUTPUT_MODE_ANGLE

// 0: 关闭调试文本（运行态仅输出协议数字） 1: 开启调试文本
#define DEBUG_SERIAL 0

#if defined(OUTPUT_MODE_BLUR) && defined(OUTPUT_MODE_ANGLE)
#error "Only one output mode can be enabled."
#endif

#if !defined(OUTPUT_MODE_BLUR) && !defined(OUTPUT_MODE_ANGLE)
#define OUTPUT_MODE_BLUR
#endif

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

// 角度有效范围(度)
const float MIN_VALID_ANGLE = -90.0;
const float MAX_VALID_ANGLE = 90.0;

// blurLevel 映射参数
const float BLUR_REF_MIN_DEG = 5.0;     // 参考跨度最小值，避免分母过小
const float BLUR_SMOOTH_ALPHA = 0.25;   // 一阶平滑系数

// ==================== EEPROM 存储结构 ====================
struct CalibData {
  uint16_t magic;     // 魔数 0xA5A5
  uint8_t version;    // 版本号 0x01
  float normal;       // 坐正角度
  float slouch;       // 驼背角度
  uint8_t checksum;   // 简单校验和
};

const uint16_t EEPROM_MAGIC = 0xA5A5;
const uint8_t EEPROM_VERSION = 0x01;

// 默认校准值
const float DEFAULT_NORMAL = 0.0;   // 默认坐正角度
const float DEFAULT_SLOUCH = 15.0;  // 默认驼背角度

// ==================== 全局变量 ====================
// MPU6050 原始数据
int16_t AcX, AcY, AcZ, Tmp, GyX, GyY, GyZ;
// 加速度与角度
float ax, ay, az, gx, gy, gz;
float angleX, angleY;

// 校准参数
float calibNormal = DEFAULT_NORMAL;
float calibSlouch = DEFAULT_SLOUCH;

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
void eepromWriteCalib(float normal, float slouch);
uint8_t calculateChecksum(const CalibData& data);
bool isValidAngle(float angle);
float performCalibration();
void processCommand(const char* cmd);
void sendAck(const char* cmdType, float angle);
void sendErr(const char* errCode);
float clampFloat(float value, float minValue, float maxValue);
int computeBlurLevel(float angle);
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

  // 校验魔数和版本
  if (data.magic != EEPROM_MAGIC || data.version != EEPROM_VERSION) {
    // 数据无效，使用默认值并初始化EEPROM
    calibNormal = DEFAULT_NORMAL;
    calibSlouch = DEFAULT_SLOUCH;
    eepromWriteCalib(calibNormal, calibSlouch);
    return;
  }

  // 校验校验和
  uint8_t storedChecksum = data.checksum;
  data.checksum = 0;
  uint8_t calcChecksum = calculateChecksum(data);

  if (storedChecksum != calcChecksum) {
    // 校验和错误，使用默认值
    calibNormal = DEFAULT_NORMAL;
    calibSlouch = DEFAULT_SLOUCH;
    eepromWriteCalib(calibNormal, calibSlouch);
    return;
  }

  // 数据有效，加载参数
  calibNormal = data.normal;
  calibSlouch = data.slouch;
}

// 写入校准数据到EEPROM
void eepromWriteCalib(float normal, float slouch) {
  CalibData data;
  data.magic = EEPROM_MAGIC;
  data.version = EEPROM_VERSION;
  data.normal = normal;
  data.slouch = slouch;
  data.checksum = 0;

  // 计算并写入校验和
  data.checksum = calculateChecksum(data);

  // 写入EEPROM
  EEPROM.put(0, data);
}

// ==================== 角度校验 ====================

// 检查角度是否在有效范围内
bool isValidAngle(float angle) {
  return (angle >= MIN_VALID_ANGLE && angle <= MAX_VALID_ANGLE);
}

// ==================== 校准执行 ====================

// 执行校准采样：500ms内采样10次求平均
float performCalibration() {
  float sum = 0;
  int validSamples = 0;

  for (int i = 0; i < CALIBRATE_SAMPLES; i++) {
    // 读取MPU6050数据
    Wire.beginTransmission(MPU_addr);
    Wire.write(0x3B);
    Wire.endTransmission(false);
    Wire.requestFrom(MPU_addr, 14, true);

    AcX = Wire.read() << 8 | Wire.read();
    AcY = Wire.read() << 8 | Wire.read();
    AcZ = Wire.read() << 8 | Wire.read();

    // 计算角度(使用Y轴角度作为姿态基准)
    ax = AcX / 16384.0;
    ay = AcY / 16384.0;
    az = AcZ / 16384.0;
    angleY = atan2(-ax, sqrt(ay*ay + az*az)) * 180 / PI;

    // 累加有效样本
    if (isValidAngle(angleY)) {
      sum += angleY;
      validSamples++;
    }

    delay(SAMPLE_INTERVAL_MS);
  }

  // 检查有效样本数量
  if (validSamples < CALIBRATE_SAMPLES / 2) {
    return NAN; // 采样不足，返回NaN表示失败
  }

  return sum / validSamples;
}

// ==================== 命令处理 ====================

// 处理串口命令
void processCommand(const char* cmd) {
  // 忙碌保护
  if (isCalibrating) {
    sendErr("BUSY");
    return;
  }

  // 匹配命令
  if (strcmp(cmd, "CMD:SET_NORMAL") == 0) {
    // 校准坐正角度
    isCalibrating = true;
    float oldNormal = calibNormal;
    float avgAngle = performCalibration();

    if (isnan(avgAngle)) {
      sendErr("CALIBRATE_TIMEOUT");
      isCalibrating = false;
      return;
    }

    if (!isValidAngle(avgAngle)) {
      sendErr("INVALID_DATA");
      isCalibrating = false;
      return;
    }

    // 更新参数
    calibNormal = avgAngle;

    // 写入EEPROM
    eepromWriteCalib(calibNormal, calibSlouch);

    // 验证写入(重新读取检查)
    CalibData verify;
    EEPROM.get(0, verify);
    if (verify.magic != EEPROM_MAGIC || fabs(verify.normal - calibNormal) > 0.01) {
      // 写入失败，回滚
      calibNormal = oldNormal;
      sendErr("EEPROM_WRITE_FAIL");
      isCalibrating = false;
      return;
    }

    sendAck("SET_NORMAL", calibNormal);
    isCalibrating = false;

  } else if (strcmp(cmd, "CMD:SET_SLOUCH") == 0) {
    // 校准驼背角度
    isCalibrating = true;
    float oldSlouch = calibSlouch;
    float avgAngle = performCalibration();

    if (isnan(avgAngle)) {
      sendErr("CALIBRATE_TIMEOUT");
      isCalibrating = false;
      return;
    }

    if (!isValidAngle(avgAngle)) {
      sendErr("INVALID_DATA");
      isCalibrating = false;
      return;
    }

    // 更新参数
    calibSlouch = avgAngle;

    // 写入EEPROM
    eepromWriteCalib(calibNormal, calibSlouch);

    // 验证写入
    CalibData verify;
    EEPROM.get(0, verify);
    if (verify.magic != EEPROM_MAGIC || fabs(verify.slouch - calibSlouch) > 0.01) {
      // 写入失败，回滚
      calibSlouch = oldSlouch;
      sendErr("EEPROM_WRITE_FAIL");
      isCalibrating = false;
      return;
    }

    sendAck("SET_SLOUCH", calibSlouch);
    isCalibrating = false;

  } else {
    // 未知命令
    sendErr("UNKNOWN_CMD");
  }
}

// ==================== 协议回包 ====================

// 发送成功回包（协议回包，始终输出）
void sendAck(const char* cmdType, float angle) {
  Serial.print("ACK:");
  Serial.print(cmdType);
  Serial.print(",ANGLE:");
  Serial.println(angle, 2); // 两位小数
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

// 将当前角度映射为 0~100 的非线性 blurLevel
int computeBlurLevel(float angle) {
  const float d = fabs(angle - calibNormal);
  const float r = fmax(fabs(calibSlouch - calibNormal), BLUR_REF_MIN_DEG);
  const float x = clampFloat(d / r, 0.0, 1.2);

  float b = 0.0;
  if (x <= 0.3) {
    b = 30.0 * pow(x / 0.3, 1.6);
  } else if (x <= 0.7) {
    b = 30.0 + 40.0 * pow((x - 0.3) / 0.4, 1.2);
  } else {
    b = 70.0 + 30.0 * pow((x - 0.7) / 0.5, 0.8);
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

  // 初始化MPU6050
  Wire.beginTransmission(MPU_addr);
  Wire.write(0x6B);
  Wire.write(0);
  Wire.endTransmission(true);

  // 从EEPROM加载校准参数
  eepromReadCalib();

  // 输出启动信息
  DBG_PRINTLN("SitRight Firmware v1.0");
  DBG_PRINT("Loaded - Normal: ");
  DBG_PRINT(calibNormal);
  DBG_PRINT(", Slouch: ");
  DBG_PRINTLN(calibSlouch);
  DBG_PRINTLN("Ready. Commands: CMD:SET_NORMAL, CMD:SET_SLOUCH");
}

void loop() {
  // ========== 串口命令处理 ==========
  while (Serial.available() > 0) {
    char c = Serial.read();

    if (c == '\n') {
      // 命令行结束
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
    // 超长命令直接丢弃
  }

  // ========== 姿态采集与上报 (保持原有逻辑) ==========
  if (!isCalibrating) {
    // 读取传感器数据
    Wire.beginTransmission(MPU_addr);
    Wire.write(0x3B);
    Wire.endTransmission(false);
    Wire.requestFrom(MPU_addr, 14, true);

    AcX = Wire.read() << 8 | Wire.read();
    AcY = Wire.read() << 8 | Wire.read();
    AcZ = Wire.read() << 8 | Wire.read();

    // 计算角度
    ax = AcX / 16384.0;
    ay = AcY / 16384.0;
    az = AcZ / 16384.0;
    angleY = atan2(-ax, sqrt(ay*ay + az*az)) * 180 / PI;

    // 运行态协议：仅输出纯数字行
  #if defined(OUTPUT_MODE_ANGLE)
    Serial.println((int)round(angleY));
  #else
    int blurRaw = computeBlurLevel(angleY);
    int blurStable = smoothBlurLevel(blurRaw);
    Serial.println(blurStable);
  #endif

    delay(DELAY_MS);
  }
}
