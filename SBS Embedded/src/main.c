/* USER CODE BEGIN Header */
/**
  ******************************************************************************
  * @file           : main.c
  * @brief          : Main program body
  ******************************************************************************
  * @attention
  *
  * Copyright (c) 2026 STMicroelectronics.
  * All rights reserved.
  *
  * This software is licensed under terms that can be found in the LICENSE file
  * in the root directory of this software component.
  * If no LICENSE file comes with this software, it is provided AS-IS.
  *
  ******************************************************************************
  */
/* USER CODE END Header */
/* Includes ------------------------------------------------------------------*/
#include "main.h"
#include "i2c.h"
#include "usart.h"
#include "gpio.h"
#include "system_control.h"
#include "button_input.h"
#include "led_display.h"
#include "lcd_wrapper.h"

/* Private includes ----------------------------------------------------------*/
/* USER CODE BEGIN Includes */

/* USER CODE END Includes */

/* Private typedef -----------------------------------------------------------*/
/* USER CODE BEGIN PTD */

/* USER CODE END PTD */

/* Private define ------------------------------------------------------------*/
/* USER CODE BEGIN PD */

/* USER CODE END PD */

/* Private macro -------------------------------------------------------------*/
/* USER CODE BEGIN PM */

/* USER CODE END PM */

/* Private variables ---------------------------------------------------------*/

/* USER CODE BEGIN PV */

// Biến lưu chu kỳ hoạt động cho chế độ AUTO
int auto_cycle = 0;
// Biến lưu chế độ hiện tại: 0=STOP, 1=AUTO, 2=MANUAL
int current_mode = 0;
// Bộ đếm chu kỳ hiện tại
int cycle_counter = 0;
// Biến lưu thời gian bắt đầu của mỗi chu kỳ
uint32_t cycle_start_time = 0;
// Thời gian thực hiện mỗi chu kỳ (ms)
#define CYCLE_DURATION 300

// Biến để theo dõi trạng thái PB3 trước đó (tránh gửi liên tục)
uint8_t pb3_previous_state = GPIO_PIN_RESET;
// Biến để theo dõi trạng thái PB4, PB8, PB9 trước đó
uint8_t pb4_previous_state = GPIO_PIN_RESET;
uint8_t pb8_previous_state = GPIO_PIN_RESET;
uint8_t pb9_previous_state = GPIO_PIN_RESET;

// Biến cho UART nhận dữ liệu
uint8_t uart_rx_char;
#define UART_RX_BUFFER_SIZE 100
uint8_t uart_rx_buffer[UART_RX_BUFFER_SIZE];
uint16_t uart_rx_index = 0;

// Biến cho chế độ AUTO qua UART
int uart_auto_cycles = 0;
int uart_auto_mode_enabled = 0;
uint8_t uart_mode_state = 0; // 0: chờ 'm', 1: chờ số, 2: chờ 'v'
int temp_cycles = 0; // Biến tạm lưu số chu kỳ

/* USER CODE END PV */

/* Private function prototypes -----------------------------------------------*/
void SystemClock_Config(void);
/* USER CODE BEGIN PFP */

/* USER CODE END PFP */

/* Private user code ---------------------------------------------------------*/
/* USER CODE BEGIN 0 */

/**
  * @brief  Start Auto function - Điều khiển các chân GPIO dựa trên trạng thái nút và cảm biến
  * @retval None
  */

  
  // Bước 3: Nếu PB8 nhận tín hiệu mức cao
 void Start_auto(void)
{
  // Bước 2: Kiểm tra các chân đầu vào
  uint8_t pb8_status = HAL_GPIO_ReadPin(GPIOB, GPIO_PIN_8);
  uint8_t pb3_status = HAL_GPIO_ReadPin(GPIOB, GPIO_PIN_3);
  uint8_t pb4_status = HAL_GPIO_ReadPin(GPIOB, GPIO_PIN_4);
  
  // Bước 3: Nếu PB8 nhận tín hiệu mức cao
  if (pb8_status == GPIO_PIN_SET)
  {
    // Hai chân PA8 và PA11 cố định mức RESET khi PB8 ở mức cao
    HAL_GPIO_WritePin(GPIOA, GPIO_PIN_8, GPIO_PIN_RESET);
    HAL_GPIO_WritePin(GPIOA, GPIO_PIN_11, GPIO_PIN_RESET);
    
    // Phân nhánh rõ ràng cho PA12 và PA15, không lo bị đè lệnh
    if ((pb3_status == GPIO_PIN_SET) && (pb4_status == GPIO_PIN_SET))
    {
      HAL_GPIO_WritePin(GPIOA, GPIO_PIN_12, GPIO_PIN_RESET);
      HAL_GPIO_WritePin(GPIOA, GPIO_PIN_15, GPIO_PIN_RESET);
    }
    else if ((pb3_status == GPIO_PIN_SET) && (pb4_status == GPIO_PIN_RESET))
    {
      HAL_GPIO_WritePin(GPIOA, GPIO_PIN_12, GPIO_PIN_RESET);
      HAL_GPIO_WritePin(GPIOA, GPIO_PIN_15, GPIO_PIN_SET);
    }
    else if ((pb3_status == GPIO_PIN_RESET) && (pb4_status == GPIO_PIN_SET))
    {
      HAL_GPIO_WritePin(GPIOA, GPIO_PIN_12, GPIO_PIN_SET);
      HAL_GPIO_WritePin(GPIOA, GPIO_PIN_15, GPIO_PIN_RESET);
    }
    else // Trường hợp cuối cùng: Cả PB3 và PB4 đều bằng RESET
    {
      HAL_GPIO_WritePin(GPIOA, GPIO_PIN_12, GPIO_PIN_SET);
      HAL_GPIO_WritePin(GPIOA, GPIO_PIN_15, GPIO_PIN_SET);
    }
  }
  else // Khi PB8 ở mức thấp
  { 
    HAL_GPIO_WritePin(GPIOA, GPIO_PIN_12, GPIO_PIN_RESET);
    HAL_GPIO_WritePin(GPIOA, GPIO_PIN_15, GPIO_PIN_RESET);
    HAL_GPIO_WritePin(GPIOA, GPIO_PIN_8, GPIO_PIN_SET);
    HAL_GPIO_WritePin(GPIOA, GPIO_PIN_11, GPIO_PIN_SET);
  }
}

/**
  * @brief  Stop/Emergency Stop function - Đặt tất cả các chân output về trạng thái ban đầu (mức thấp)
  * @retval None
  */
// Định nghĩa các trạng thái cho dễ đọc code
#define STATE_STOP  0
#define STATE_RUN   1

uint8_t system_state = STATE_STOP; // Mới bật nguồn thì luôn luôn DỪNG để an toàn

void Stop_auto(void)
{
  // Đặt tất cả các chân output về mức thấp (Emergency Stop)
  HAL_GPIO_WritePin(GPIOA, GPIO_PIN_8, GPIO_PIN_RESET);
  HAL_GPIO_WritePin(GPIOA, GPIO_PIN_11, GPIO_PIN_RESET);
  HAL_GPIO_WritePin(GPIOA, GPIO_PIN_12, GPIO_PIN_RESET);
  HAL_GPIO_WritePin(GPIOA, GPIO_PIN_15, GPIO_PIN_RESET);
}

/**
  * @brief  Auto Mode - Thực hiện Start_auto nhiều lần dựa trên chu kỳ
  * @param  cycle: Số lần thực hiện. Nếu cycle <= 0 thì hoạt động liên tục
  * @retval None
  */
void auto_mode(int cycle)
{
  extern SystemState_t g_system;
  auto_cycle = cycle;        // Lưu chu kỳ
  cycle_counter = 0;         // Reset bộ đếm
  cycle_start_time = HAL_GetTick(); // Lưu thời gian bắt đầu
  current_mode = 1;          // Đặt chế độ thành AUTO
  g_system.is_running = 1;   // Hệ thống bắt đầu chạy
}

/**
  * @brief  Manual Mode - Cho phép điều khiển thủ công
  * @retval None
  */
void manual_mode(void)
{
  current_mode = 2;          // Đặt chế độ thành MANUAL
  cycle_counter = 0;         // Reset bộ đếm
  auto_cycle = 0;            // Reset chu kỳ
  // Trong chế độ MANUAL, người dùng điều khiển trực tiếp qua các cảm biến PB3, PB4, PB8
}

/**
  * @brief  Xử lý dữ liệu nhận từ UART
  * @retval None
  */
void Process_UART_Command(void)
{
  // Nhận một ký tự từ UART
  if (HAL_UART_Receive(&huart1, &uart_rx_char, 1, 10) == HAL_OK)
  {
    // Xử lý các trạng thái khác nhau
    if (uart_mode_state == 0) // Chờ 'm'
    {
      if (uart_rx_char == 'm')
      {
        uart_rx_index = 0;
        uart_mode_state = 1; // Chuyển sang chờ số
      }
    }
    else if (uart_mode_state == 1) // Chờ nhập số
    {
      if (uart_rx_char >= '0' && uart_rx_char <= '9')
      {
        // Nhập số
        if (uart_rx_index < UART_RX_BUFFER_SIZE - 1)
        {
          uart_rx_buffer[uart_rx_index++] = uart_rx_char;
        }
      }
      else if (uart_rx_char == '\r' || uart_rx_char == '\n')
      {
        // Kết thúc nhập số
        if (uart_rx_index > 0)
        {
          uart_rx_buffer[uart_rx_index] = '\0'; // Kết thúc chuỗi
          
          // Thử chuyển đổi chuỗi thành số nguyên
          int value = 0;
          int is_valid = 1;
          
          for (int i = 0; i < uart_rx_index; i++)
          {
            if (uart_rx_buffer[i] >= '0' && uart_rx_buffer[i] <= '9')
            {
              value = value * 10 + (uart_rx_buffer[i] - '0');
            }
            else
            {
              is_valid = 0;
              break;
            }
          }
          
          if (is_valid && value > 0)
          {
            temp_cycles = value; // Lưu giá trị vào biến tạm
            uart_mode_state = 2; // Chuyển sang chờ 'v'
            uart_rx_index = 0;
          }
          else
          {
            // Số không hợp lệ, quay lại chờ 'm'
            uart_mode_state = 0;
            uart_rx_index = 0;
          }
        }
      }
      else
      {
        // Ký tự không hợp lệ, quay lại chờ 'm'
        uart_mode_state = 0;
        uart_rx_index = 0;
      }
    }
    else if (uart_mode_state == 2) // Chờ 'v'
    {
      if (uart_rx_char == 'v')
      {
        extern SystemState_t g_system;
        // Kích hoạt chế độ AUTO với số chu kỳ từ UART
        auto_mode(temp_cycles);
        g_system.is_running = 1;  // Hệ thống bắt đầu chạy
        uart_auto_mode_enabled = 1; // Kích hoạt chế độ UART auto
        cycle_start_time = HAL_GetTick();
        uart_mode_state = 0; // Quay lại chờ 'm'
        uart_rx_index = 0;
      }
      else if (uart_rx_char == '\r' || uart_rx_char == '\n')
      {
        // Bỏ qua ký tự xuống dòng
      }
      else
      {
        // Ký tự không hợp lệ, quay lại chờ 'm'
        uart_mode_state = 0;
        uart_rx_index = 0;
      }
    }
  }
}


void handle_auto_mode(void)
{
  extern SystemState_t g_system;
  
  // Nếu chu kỳ > 0: thực hiện với giới hạn chu kỳ
  if (auto_cycle > 0)
  {
    // Kiểm tra nếu hết khoảng thời gian của chu kỳ hiện tại
    if (HAL_GetTick() - cycle_start_time >= CYCLE_DURATION)
    {
      cycle_counter++; // Tăng bộ đếm
      cycle_start_time = HAL_GetTick(); // Reset thời gian bắt đầu chu kỳ mới
      
      // Nếu đã hoàn thành tất cả chu kỳ
      if (cycle_counter >= auto_cycle)
      {
        current_mode = 0; // Trở về chế độ STOP
        g_system.is_running = 0;  // Hệ thống dừng
        Stop_auto(); // Dừng hệ thống
        return;
      }
    }
  }
  
  // Chỉ gọi Start_auto() khi hệ thống đang chạy
  if (g_system.is_running)
  {
    Start_auto();
  }
}

/**
  * @brief  Tool Auto Mode - Chế độ AUTO qua UART (không kiểm tra nút PA0)
  * @retval None
  */
void tool_auto_mode(void)
{
  extern SystemState_t g_system;
  
  // Nếu chu kỳ > 0: thực hiện với giới hạn chu kỳ
  if (temp_cycles > 0)
  {
    // Kiểm tra nếu hết khoảng thời gian của chu kỳ hiện tại
    if (HAL_GetTick() - cycle_start_time >= CYCLE_DURATION)
    {
      cycle_counter++; // Tăng bộ đếm
      temp_cycles--;   // Giảm số chu kỳ còn lại
      cycle_start_time = HAL_GetTick(); // Reset thời gian bắt đầu chu kỳ mới
      
      // Nếu đã hoàn thành tất cả chu kỳ
      if (temp_cycles == 0)
      {
        current_mode = 0; // Trở về chế độ STOP
        Stop_auto(); // Dừng hệ thống
        UART1_SendString("h"); // Gửi "h\r\n" khi hoàn thành
        uart_auto_mode_enabled = 0; // Vô hiệu hóa chế độ UART auto
        g_system.is_running = 0;  // Hệ thống dừng
        return;
      }
    }
  }
  
  // Chỉ gọi Start_auto() khi hệ thống đang chạy
  if (g_system.is_running)
  {
    Start_auto();
  }
}

void Display_No(uint8_t number) {
    // Đảm bảo số chỉ nằm trong khoảng 0-9 (4 bit)
    number &= 0x0F; 

    // Cách 1: Dùng thư viện HAL (An toàn, dễ hiểu)
    HAL_GPIO_WritePin(GPIOB, GPIO_PIN_12, (number >> 0) & 0x01);
    HAL_GPIO_WritePin(GPIOB, GPIO_PIN_13, (number >> 1) & 0x01);
    HAL_GPIO_WritePin(GPIOB, GPIO_PIN_14, (number >> 2) & 0x01);
    HAL_GPIO_WritePin(GPIOB, GPIO_PIN_15, (number >> 3) & 0x01);
}
/**
  * @brief  Handle Manual Mode - Xử lý logic chế độ MANUAL
  * @retval None
  */
void handle_manual_mode(void)
{
  extern SystemState_t g_system;
  
  // Nếu chu kỳ > 0: thực hiện với giới hạn chu kỳ
  if (g_system.auto_cycle > 0)
  {
    // Kiểm tra nếu hết khoảng thời gian của chu kỳ hiện tại
    if (HAL_GetTick() - g_system.cycle_start_time >= CYCLE_DURATION)
    {
      g_system.cycle_counter++; // Tăng bộ đếm
      g_system.cycle_start_time = HAL_GetTick(); // Reset thời gian bắt đầu chu kỳ mới
      
      // Nếu đã hoàn thành tất cả chu kỳ
      if (g_system.cycle_counter >= g_system.auto_cycle)
      {
        g_system.current_mode = MODE_STOP; // Trở về chế độ STOP
        g_system.is_running = 0;  // Hệ thống dừng
        Stop_auto(); // Dừng hệ thống
        return;
      }
    }
  }
  
  // Chỉ gọi Start_auto() khi hệ thống đang chạy
  if (g_system.is_running)
  {
    Start_auto();
  }
}

/* USER CODE END 0 */

/**
  * @brief  The application entry point.
  * @retval int
  */
int main(void)
{

  /* USER CODE BEGIN 1 */

  /* USER CODE END 1 */

  /* MCU Configuration--------------------------------------------------------*/

  /* Reset of all peripherals, Initializes the Flash interface and the Systick. */
  HAL_Init();

  /* USER CODE BEGIN Init */

  /* USER CODE END Init */

  /* Configure the system clock */
  SystemClock_Config();

  /* USER CODE BEGIN SysInit */

  /* USER CODE END SysInit */

  /* Initialize all configured peripherals */
  MX_GPIO_Init();
  MX_I2C1_Init();
  MX_USART1_UART_Init();
  /* USER CODE BEGIN 2 */
  
  /* Initialize system libraries */
  System_Init();
  Button_Init();
  LED_Init();
  LCD_Init();
  LCD_Clear();
  LCD_SetCursor(0, 0);
  LCD_Print("System Ready");
  LCD_SetCursor(1, 0);
  LCD_Print("SBS Team!");

  /* USER CODE END 2 */
  /* Infinite loop */
  /* USER CODE BEGIN WHILE */
  while (1)
  {
    /* USER CODE END WHILE */
    /* Set LED common cathode to LOW (enable LED) */
    LED_SetCommonCathode(GPIO_PIN_RESET);
    
    /* Display number 1 on 74LS47 decoder */
    LED_DisplayNumber(1);
    HAL_Delay(100);  // Delay for 74LS47 to decode BCD
    
    /* Process UART command */
    Process_UART_Command();
    
    /* Process button inputs */
    Button_Process();
    
    /* Execute current system mode */
    System_Execute();
    
    /* USER CODE BEGIN 3 */
  }
  /* USER CODE END 3 */
}/**

  * @brief System Clock Configuration
  * @retval None
  */
void SystemClock_Config(void)
{
  RCC_OscInitTypeDef RCC_OscInitStruct = {0};
  RCC_ClkInitTypeDef RCC_ClkInitStruct = {0};

  /** Initializes the RCC Oscillators according to the specified parameters
  * in the RCC_OscInitTypeDef structure.
  */
  RCC_OscInitStruct.OscillatorType = RCC_OSCILLATORTYPE_HSE;
  RCC_OscInitStruct.HSEState = RCC_HSE_ON;
  RCC_OscInitStruct.HSEPredivValue = RCC_HSE_PREDIV_DIV1;
  RCC_OscInitStruct.HSIState = RCC_HSI_ON;
  RCC_OscInitStruct.PLL.PLLState = RCC_PLL_ON;
  RCC_OscInitStruct.PLL.PLLSource = RCC_PLLSOURCE_HSE;
  RCC_OscInitStruct.PLL.PLLMUL = RCC_PLL_MUL9;
  if (HAL_RCC_OscConfig(&RCC_OscInitStruct) != HAL_OK)
  {
    Error_Handler();
  }

  /** Initializes the CPU, AHB and APB buses clocks
  */
  RCC_ClkInitStruct.ClockType = RCC_CLOCKTYPE_HCLK|RCC_CLOCKTYPE_SYSCLK
                              |RCC_CLOCKTYPE_PCLK1|RCC_CLOCKTYPE_PCLK2;
  RCC_ClkInitStruct.SYSCLKSource = RCC_SYSCLKSOURCE_PLLCLK;
  RCC_ClkInitStruct.AHBCLKDivider = RCC_SYSCLK_DIV1;
  RCC_ClkInitStruct.APB1CLKDivider = RCC_HCLK_DIV2;
  RCC_ClkInitStruct.APB2CLKDivider = RCC_HCLK_DIV1;

  if (HAL_RCC_ClockConfig(&RCC_ClkInitStruct, FLASH_LATENCY_2) != HAL_OK)
  {
    Error_Handler();
  }
}

/* USER CODE BEGIN 4 */

/* USER CODE END 4 */

/**
  * @brief  This function is executed in case of error occurrence.
  * @retval None
  */
void Error_Handler(void)
{
  /* USER CODE BEGIN Error_Handler_Debug */
  /* User can add his own implementation to report the HAL error return state */
  __disable_irq();
  while (1)
  {
  }
  /* USER CODE END Error_Handler_Debug */
}
#ifdef USE_FULL_ASSERT
/**
  * @brief  Reports the name of the source file and the source line number
  *         where the assert_param error has occurred.
  * @param  file: pointer to the source file name
  * @param  line: assert_param error line source number
  * @retval None
  */
void assert_failed(uint8_t *file, uint32_t line)
{
  /* USER CODE BEGIN 6 */
  /* User can add his own implementation to report the file name and line number,
     ex: printf("Wrong parameters value: file %s on line %d\r\n", file, line) */
  /* USER CODE END 6 */
}
#endif /* USE_FULL_ASSERT */
