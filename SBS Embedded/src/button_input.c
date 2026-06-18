#include "button_input.h"

/* Global button states (track previous states for edge detection) */
ButtonStates_t g_buttons = {0};

static uint8_t pb3_prev = GPIO_PIN_RESET;
static uint8_t pb4_prev = GPIO_PIN_RESET;
static uint8_t pb8_prev = GPIO_PIN_RESET;
static uint8_t pb9_prev = GPIO_PIN_RESET;
static uint8_t pa0_prev = GPIO_PIN_RESET;
static uint8_t pa1_prev = GPIO_PIN_RESET;
static uint8_t pa2_prev = GPIO_PIN_RESET;
static uint8_t pa3_prev = GPIO_PIN_RESET;

/* External functions from main.c */
extern void UART1_SendString(const char *str);
extern void System_SetAutoMode(int cycles);
extern void System_SetManualMode(int cycles);
extern void System_SetStopMode(void);

/**
 * @brief Initialize button state tracking
 */
void Button_Init(void)
{
    pb3_prev = GPIO_PIN_RESET;
    pb4_prev = GPIO_PIN_RESET;
    pb8_prev = GPIO_PIN_RESET;
    pb9_prev = GPIO_PIN_RESET;
    pa0_prev = GPIO_PIN_RESET;
    pa1_prev = GPIO_PIN_RESET;
    pa2_prev = GPIO_PIN_RESET;
    pa3_prev = GPIO_PIN_RESET;
}

/**
 * @brief Check rising edge on specific button (LOW -> HIGH)
 */
uint8_t Button_CheckRisingEdge(GPIO_TypeDef *port, uint16_t pin, uint8_t *prev_state)
{
    uint8_t current_state = HAL_GPIO_ReadPin(port, pin);
    if ((current_state == GPIO_PIN_SET) && (*prev_state == GPIO_PIN_RESET))
    {
        *prev_state = current_state;
        HAL_Delay(20);  // Debounce
        return 1;  // Rising edge detected
    }
    *prev_state = current_state;
    return 0;
}

/**
 * @brief Check falling edge on specific button (HIGH -> LOW)
 */
uint8_t Button_CheckFallingEdge(GPIO_TypeDef *port, uint16_t pin, uint8_t *prev_state)
{
    uint8_t current_state = HAL_GPIO_ReadPin(port, pin);
    if ((current_state == GPIO_PIN_RESET) && (*prev_state == GPIO_PIN_SET))
    {
        *prev_state = current_state;
        HAL_Delay(20);  // Debounce
        return 1;  // Falling edge detected
    }
    *prev_state = current_state;
    return 0;
}

/**
 * @brief Handle button inputs (call this in main loop)
 */
void Button_Process(void)
{
    /* PB3: Send "a" on rising edge */
    if (Button_CheckRisingEdge(GPIOB, GPIO_PIN_3, &pb3_prev))
    {
        UART1_SendString("a");
    }

    /* PB4: Send "b" on rising edge */
    if (Button_CheckRisingEdge(GPIOB, GPIO_PIN_4, &pb4_prev))
    {
        UART1_SendString("b");
    }

    /* PB8: Send "c" on rising edge */
    if (Button_CheckRisingEdge(GPIOB, GPIO_PIN_8, &pb8_prev))
    {
        UART1_SendString("c");
    }

    /* PB9: Send "d" on rising edge */
    if (Button_CheckRisingEdge(GPIOB, GPIO_PIN_9, &pb9_prev))
    {
        UART1_SendString("d");
    }

    /* PA0: AUTO mode (with PA2) */
    uint8_t pa0_current = HAL_GPIO_ReadPin(GPIOA, GPIO_PIN_0);
    uint8_t pa2_current = HAL_GPIO_ReadPin(GPIOA, GPIO_PIN_2);
    if ((pa2_current == GPIO_PIN_SET) && (pa0_current == GPIO_PIN_SET) && (pa0_prev == GPIO_PIN_RESET))
    {
        System_SetAutoMode(2);  // AUTO mode with 2 cycles
        HAL_Delay(100);
    }
    pa0_prev = pa0_current;

    /* PA2: Already handled with PA0 */
    pa2_prev = pa2_current;

    /* PA3: MANUAL mode */
    if (Button_CheckRisingEdge(GPIOA, GPIO_PIN_3, &pa3_prev))
    {
        System_SetManualMode(2);  // MANUAL mode with 2 cycles
        HAL_Delay(100);
    }

    /* PA1: STOP mode (highest priority) */
    if (HAL_GPIO_ReadPin(GPIOA, GPIO_PIN_1) == GPIO_PIN_SET)
    {
        System_SetStopMode();
        HAL_Delay(100);
    }
}
