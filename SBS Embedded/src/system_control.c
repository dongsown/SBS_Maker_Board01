#include "system_control.h"
#include "usart.h"

/* Global system state */
SystemState_t g_system = {
    .current_mode = MODE_STOP,
    .auto_cycle = 0,
    .cycle_counter = 0,
    .cycle_start_time = 0,
    .uart_auto_enabled = 0,
    .temp_cycles = 0,
    .is_running = 0  // Mặc định hệ thống không chạy
};

/* External functions from main.c */
extern void Start_auto(void);
extern void Stop_auto(void);
extern void handle_auto_mode(void);
extern void tool_auto_mode(void);
extern void handle_manual_mode(void);

/**
 * @brief Initialize system state
 */
void System_Init(void)
{
    g_system.current_mode = MODE_STOP;
    g_system.auto_cycle = 0;
    g_system.cycle_counter = 0;
    g_system.uart_auto_enabled = 0;
    g_system.is_running = 0;  // Hệ thống chưa chạy
}

/**
 * @brief Execute current system mode
 */
void System_Execute(void)
{
    if (g_system.uart_auto_enabled)
    {
        /* UART AUTO mode is priority */
        tool_auto_mode();
    }
    else
    {
        /* Execute based on current mode */
        switch (g_system.current_mode)
        {
            case MODE_STOP:
                Stop_auto();
                break;
            
            case MODE_AUTO:
                handle_auto_mode();
                break;
            
            case MODE_MANUAL:
                handle_manual_mode();
                break;
            
            default:
                Stop_auto();
                break;
        }
    }
}

/**
 * @brief Set AUTO mode with cycles
 * @param cycles: Number of cycles
 */
void System_SetAutoMode(int cycles)
{
    g_system.auto_cycle = cycles;
    g_system.cycle_counter = 0;
    g_system.cycle_start_time = HAL_GetTick();
    g_system.current_mode = MODE_AUTO;
    g_system.is_running = 1;  // Hệ thống bắt đầu chạy
}

/**
 * @brief Set MANUAL mode with cycles
 * @param cycles: Number of cycles
 */
void System_SetManualMode(int cycles)
{
    g_system.current_mode = MODE_MANUAL;
    g_system.auto_cycle = cycles;  /* Reuse auto_cycle for manual cycles */
    g_system.cycle_counter = 0;
    g_system.cycle_start_time = HAL_GetTick();
    g_system.is_running = 1;  // Hệ thống bắt đầu chạy
}

/**
 * @brief Set STOP mode (Emergency Stop)
 */
void System_SetStopMode(void)
{
    g_system.current_mode = MODE_STOP;
    g_system.uart_auto_enabled = 0;
    g_system.is_running = 0;  // Hệ thống dừng
    Stop_auto();
}

/**
 * @brief Set AUTO mode from UART
 * @param cycles: Number of cycles
 */
void System_SetUARTAutoMode(int cycles)
{
    g_system.temp_cycles = cycles;
    g_system.cycle_counter = 0;
    g_system.cycle_start_time = HAL_GetTick();
    g_system.uart_auto_enabled = 1;
    g_system.current_mode = MODE_STOP;
}
