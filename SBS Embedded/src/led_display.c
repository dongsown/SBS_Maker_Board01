#include "led_display.h"

/**
 * @brief Initialize LED display
 */
void LED_Init(void)
{
    /* Set common cathode to LOW (enable LED display) */
    HAL_GPIO_WritePin(GPIOB, GPIO_PIN_1, GPIO_PIN_RESET);
    HAL_GPIO_WritePin(GPIOB, GPIO_PIN_2, GPIO_PIN_RESET);
    
    /* Clear all segments initially */
    HAL_GPIO_WritePin(GPIOB, GPIO_PIN_12, GPIO_PIN_RESET);
    HAL_GPIO_WritePin(GPIOB, GPIO_PIN_13, GPIO_PIN_RESET);
    HAL_GPIO_WritePin(GPIOB, GPIO_PIN_14, GPIO_PIN_RESET);
    HAL_GPIO_WritePin(GPIOB, GPIO_PIN_15, GPIO_PIN_RESET);
    
    HAL_Delay(10);  // Delay for 74LS47 to stabilize
}

/**
 * @brief Display a number (0-9) on 4-bit LED
 * @param number: Number to display (0-9, 4-bit)
 */
void LED_DisplayNumber(uint8_t number)
{
    /* Ensure number is in range 0-15 (4-bit) */
    number &= 0x0F;

    /* Write each bit to corresponding GPIO pin (BCD input for 74LS47) */
    HAL_GPIO_WritePin(GPIOB, GPIO_PIN_12, (number >> 0) & 0x01);
    HAL_GPIO_WritePin(GPIOB, GPIO_PIN_13, (number >> 1) & 0x01);
    HAL_GPIO_WritePin(GPIOB, GPIO_PIN_14, (number >> 2) & 0x01);
    HAL_GPIO_WritePin(GPIOB, GPIO_PIN_15, (number >> 3) & 0x01);
    
    /* Small delay for 74LS47 to decode BCD and stabilize output */
    HAL_Delay(5);
}

/**
 * @brief Set LED common cathode pins (PB1, PB2)
 * @param state: GPIO_PIN_SET or GPIO_PIN_RESET
 */
void LED_SetCommonCathode(GPIO_PinState state)
{
    HAL_GPIO_WritePin(GPIOB, GPIO_PIN_1, state);
    HAL_GPIO_WritePin(GPIOB, GPIO_PIN_2, state);
}

/**
 * @brief Clear LED display (all segments off)
 */
void LED_Clear(void)
{
    HAL_GPIO_WritePin(GPIOB, GPIO_PIN_12, GPIO_PIN_RESET);
    HAL_GPIO_WritePin(GPIOB, GPIO_PIN_13, GPIO_PIN_RESET);
    HAL_GPIO_WritePin(GPIOB, GPIO_PIN_14, GPIO_PIN_RESET);
    HAL_GPIO_WritePin(GPIOB, GPIO_PIN_15, GPIO_PIN_RESET);
}
