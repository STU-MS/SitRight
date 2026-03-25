/*
 * 功能说明:
 * - 读取角度传感器数据
 * - 三级坐姿状态判断
 * - LED 灯光提醒
 * - 串口发送状态数据
 */

// 坐姿矫正仪 - 测试代码
const int LED_R = 9;
const int LED_G = 10;
const int LED_B = 11;
const int ANGLE_PIN = A0;

int threshold1 = 300;
int threshold2 = 600;
int currentState = 0;
int angleValue = 0;

void setup() {
  Serial.begin(9600);
  pinMode(LED_R, OUTPUT);
  pinMode(LED_G, OUTPUT);
  pinMode(LED_B, OUTPUT);
  Serial.println("坐姿矫正仪启动");
}

void loop() {
  // 读取角度值
  angleValue = analogRead(ANGLE_PIN);
  
  // 判断坐姿状态
  if (angleValue < threshold1) {
    currentState = 0;  // 端正
    digitalWrite(LED_R, LOW);
    digitalWrite(LED_G, HIGH);
    digitalWrite(LED_B, LOW);
  } 
  else if (angleValue < threshold2) {
    currentState = 1;  // 轻度不端正
    digitalWrite(LED_R, LOW);
    digitalWrite(LED_G, LOW);
    digitalWrite(LED_B, HIGH);
  } 
  else {
    currentState = 2;  // 重度不端正
    digitalWrite(LED_R, HIGH);
    digitalWrite(LED_G, LOW);
    digitalWrite(LED_B, LOW);
  }
  
  // 发送数据到串口
  Serial.print("STATE:");
  Serial.print(currentState);
  Serial.print(",ANGLE:");
  Serial.println(angleValue);
  
  delay(100);
}