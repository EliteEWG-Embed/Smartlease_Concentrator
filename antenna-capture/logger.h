#ifndef LOGGER_H
#define LOGGER_H

extern int log_console_enabled;

#pragma once

void init_logger();
void log_info(const char *format, ...);
void log_error(const char *format, ...);

#define LOG_INFO(fmt, ...)  if (log_console_enabled) printf("[INFO] " fmt "\n", ##__VA_ARGS__)
#define LOG_ERROR(fmt, ...) if (log_console_enabled) fprintf(stderr, "[ERROR] " fmt "\n", ##__VA_ARGS__)

#endif
