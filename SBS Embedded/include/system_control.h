#ifndef SYSTEM_CONTROL_H
#define SYSTEM_CONTROL_H

#include "stm32f1xx_hal.h"

/* Mode definitions */
#define MODE_STOP       0
#define MODE_AUTO       1
#define MODE_MANUAL     2

/* Cycle duration */
#define CYCLE_DURATION  300  // ms

/* System state variables */
typedef struct {
    int current_mode;           // Chế độ hiện tại (STOP/AUTO/MANUAL)
    int auto_cycle;             // Số chu kỳ
    int cycle_counter;          // Bộ đếm chu kỳ
    uint32_t cycle_start_time;  // Thời gian bắt đầu chu kỳ
    uint8_t uart_auto_enabled;  // Cờ chế độ UART AUTO
    int temp_cycles;            // Số chu kỳ tạm từ UART
    uint8_t is_running;         // Cờ hệ thống đang chạy (1=chạy, 0=dừng)
} SystemState_t;

/* External system state */
extern SystemState_t g_system;

/**
 * @brief Initialize system state
 */
void System_Init(void);

/**
 * @brief Execute current system mode
 */
void System_Execute(void);

/**
 * @brief Set AUTO mode with cycles
 * @param cycles: Number of cycles (0 = continuous)
 */
void System_SetAutoMode(int cycles);

/**
 * @brief Set MANUAL mode with cycles
 * @param cycles: Number of cycles
 */
void System_SetManualMode(int cycles);

/**
 * @brief Set STOP mode (Emergency Stop)
 */
void System_SetStopMode(void);

/**
 * @brief Set AUTO mode from UART
 * @param cycles: Number of cycles
 */
void System_SetUARTAutoMode(int cycles);

#endif /* SYSTEM_CONTROL_H */
