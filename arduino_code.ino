// 坐姿矫正仪 

const int LED_R = 9;
const int LED_G = 10;
const int LED_B = 11;
const int ANGLE_PIN = A0;
const int BUTTON_PIN = 3;

// 用户可调阈值
int normalThreshold = 300;  // 正常阈值（角度 ≤ 此值 → 模糊度0%）
int maxThreshold = 800;     // 最大阈值（角度 ≥ 此值 → 模糊度100%）

int angleValue = 0;
int blurLevel = 0;  // 模糊度 0-100

void setup() {
  Serial.begin(9600);
  pinMode(LED_R, OUTPUT);
  pinMode(LED_G, OUTPUT);
  pinMode(LED_B, OUTPUT);
  pinMode(BUTTON_PIN, INPUT_PULLUP);
  
  Serial.println("坐姿矫正仪启动 - 连续模糊度版本");
  Serial.print("正常阈值: ");
  Serial.print(normalThreshold);
  Serial.print(", 最大阈值: ");
  Serial.println(maxThreshold);
}

void loop() {
  // 1. 读取角度值
  angleValue = analogRead(ANGLE_PIN);
  
  // 2. 计算模糊度 (0-100)
  blurLevel = calculateBlur(angleValue, normalThreshold, maxThreshold);
  
  // 3. 控制 LED 渐变 (绿→黄→红)
  controlLEDbyBlur(blurLevel);
  
  // 4. 发送数据到串口
  Serial.print("ANGLE:");
  Serial.print(angleValue);
  Serial.print(",BLUR:");
  Serial.println(blurLevel);
  
  // 5. 按键调节阈值
  handleButton();
  
  delay(100);
}

int calculateBlur(int angle, int normal, int max) {
  // 角度 ≤ 正常阈值 → 模糊度 0%
  if (angle <= normal) {
    return 0;
  }
  // 角度 ≥ 最大阈值 → 模糊度 100%
  else if (angle >= max) {
    return 100;
  }
  // 中间值 → 线性映射 0~100%
  else {
    return (angle - normal) * 100 / (max - normal);
  }
}

void controlLEDbyBlur(int blur) {
  // 模糊度 0%   → 绿灯亮 (R=0, G=255, B=0)
  // 模糊度 50%  → 黄灯亮 (R=255, G=255, B=0)
  // 模糊度 100% → 红灯亮 (R=255, G=0, B=0)
  
  int redValue = 0;
  int greenValue = 0;
  
  if (blur <= 50) {
    // 0-50%: 绿 → 黄
    // 绿从 255 降到 0，红从 0 升到 255
    int ratio = blur * 2;  // 0-100
    redValue = map(ratio, 0, 100, 0, 255);
    greenValue = map(ratio, 0, 100, 255, 0);
  } else {
    // 50-100%: 黄 → 红
    // 红保持 255，绿从 255 降到 0
    int ratio = (blur - 50) * 2;  // 0-100
    redValue = 255;
    greenValue = map(ratio, 0, 100, 255, 0);
  }
  
  analogWrite(LED_R, redValue);
  analogWrite(LED_G, greenValue);
  analogWrite(LED_B, 0);  // 蓝灯不用
}

void handleButton() {
  if (digitalRead(BUTTON_PIN) == LOW) {
    delay(50);
    if (digitalRead(BUTTON_PIN) == LOW) {
      // 按一次，交替调节 normal 和 max
      static int mode = 0;
      if (mode == 0) {
        normalThreshold += 50;
        if (normalThreshold > maxThreshold - 50) normalThreshold = maxThreshold - 50;
        Serial.print("正常阈值已调整为: ");
        Serial.println(normalThreshold);
      } else {
        maxThreshold += 50;
        if (maxThreshold > 1023) maxThreshold = 1023;
        Serial.print("最大阈值已调整为: ");
        Serial.println(maxThreshold);
      }
      mode = 1 - mode;
      delay(300);
    }
  }
}