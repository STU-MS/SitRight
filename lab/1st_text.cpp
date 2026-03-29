#include <Wire.h>
const int MPU_addr = 0x68;  // MPU6050 地址
int16_t AcX, AcY, AcZ, Tmp, GyX, GyY, GyZ;
float ax, ay, az, gx, gy, gz;
float angleX, angleY;

void setup() {
  Wire.begin();
  Serial.begin(115200);  // 波特率 115200
  
  // 初始化MPU6050
  Wire.beginTransmission(MPU_addr);
  Wire.write(0x6B);
  Wire.write(0);
  Wire.endTransmission(true);
}

void loop() {
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
  angleX = atan2(ay, az) * 180 / PI;
  angleY = atan2(-ax, sqrt(ay*ay + az*az)) * 180 / PI;

  // 输出数据
  Serial.print("X轴角度: ");
  Serial.print(angleX);
  Serial.print("  |  Y轴角度: ");
  Serial.println(angleY);

  delay(200);  // 200ms 延迟
}
