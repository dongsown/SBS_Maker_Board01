#ifndef LCD_WRAPPER_H
#define LCD_WRAPPER_H

#include "i2c_lcd.h"

/**
 * @brief Initialize LCD display
 */
void LCD_Init(void);

/**
 * @brief Clear LCD display
 */
void LCD_Clear(void);

/**
 * @brief Set cursor position
 * @param row: Row (0 or 1)
 * @param col: Column (0-15)
 */
void LCD_SetCursor(uint8_t row, uint8_t col);

/**
 * @brief Print string to LCD
 * @param str: String to print
 */
void LCD_Print(const char *str);

#endif /* LCD_WRAPPER_H */
