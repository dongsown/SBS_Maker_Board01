#ifndef LED_DISPLAY_H
#define LED_DISPLAY_H

#include "stm32f1xx_hal.h"

/**
 * @brief Initialize LED display
 */
void LED_Init(void);

/**
 * @brief Display a number (0-9) on 4-bit LED
 * @param number: Number to display (0-9, 4-bit)
 */
void LED_DisplayNumber(uint8_t number);

/**
 * @brief Set LED common cathode pins (PB1, PB2)
 * @param state: GPIO_PIN_SET or GPIO_PIN_RESET
 */
void LED_SetCommonCathode(GPIO_PinState state);

/**
 * @brief Clear LED display (all segments off)
 */
void LED_Clear(void);

#endif /* LED_DISPLAY_H */
