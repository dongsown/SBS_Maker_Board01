#include "lcd_wrapper.h"
#include "i2c.h"

/* Global LCD handle */
static I2C_LCD_HandleTypeDef g_lcd = {0};

void LCD_Init(void)
{
    g_lcd.hi2c = &hi2c1;
    g_lcd.address = 0x27;  // I2C address of LCD (7-bit)
    lcd_init(&g_lcd);
    HAL_Delay(200);
}

void LCD_Clear(void)
{
    lcd_clear(&g_lcd);
}

void LCD_SetCursor(uint8_t row, uint8_t col)
{
    lcd_gotoxy(&g_lcd, col, row);
}

void LCD_Print(const char *str)
{
    if (str != NULL)
    {
        lcd_puts(&g_lcd, (char *)str);
    }
}
