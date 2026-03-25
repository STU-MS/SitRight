#include <Wire.h>
#include <MPU6050.h>
#include <DataRecorder.h> // 模拟Linkboy数据记录器

MPU6050 mpu;
DataRecorder recorder;

void setup() {
  Serial.begin(9600);
  // 运动传感器初始化
  if (!mpu.begin()) {
    while (1);
  }
  // 清空数据记录器
  recorder.clear();
}

void loop() {
  // 读取角度X
  float angleX = mpu.getAngleX();
  recorder.add(angleX);
  Serial.println(angleX);

  // 读取角度Y
  float angleY = mpu.getAngleY();
  recorder.add(angleY);
  Serial.println(angleY);

  delay(200);
}
