#ifndef BUTTON_INPUT_H
#define BUTTON_INPUT_H

#include "stm32f1xx_hal.h"

/* Button state tracking */
typedef struct {
    uint8_t pb3_state;
    uint8_t pb4_state;
    uint8_t pb8_state;
    uint8_t pb9_state;
    uint8_t pa0_state;
    uint8_t pa1_state;
    uint8_t pa2_state;
    uint8_t pa3_state;
} ButtonStates_t;

extern ButtonStates_t g_buttons;

/**
 * @brief Initialize button state tracking
 */
void Button_Init(void);

/**
 * @brief Handle button inputs (call this in main loop)
 */
void Button_Process(void);

/**
 * @brief Check rising edge on specific button
 * @param port: GPIO port
 * @param pin: GPIO pin
 * @param prev_state: Previous state pointer
 * @return 1 if rising edge detected, 0 otherwise
 */
uint8_t Button_CheckRisingEdge(GPIO_TypeDef *port, uint16_t pin, uint8_t *prev_state);

/**
 * @brief Check falling edge on specific button
 * @param port: GPIO port
 * @param pin: GPIO pin
 * @param prev_state: Previous state pointer
 * @return 1 if falling edge detected, 0 otherwise
 */
uint8_t Button_CheckFallingEdge(GPIO_TypeDef *port, uint16_t pin, uint8_t *prev_state);

#endif /* BUTTON_INPUT_H */
