#ifndef CAPTURE_H
#define CAPTURE_H

#define _POSIX_C_SOURCE 199309L 

#include <stdio.h>
#include <fcntl.h>
#include <time.h>
#include "message.h"

#define SLEEP_DURATION_MICROSECONDS 1000000

// Transceiver register values
#define RX_REGISTER_VALUES {0x00, 'l', 0x0E, 'D', 0x0B, 0x0B, 0xE7, 0xE7, 0xE7, 0xE7, 0xD8}

// Serial port
#define DEFAULT_SERIAL_PORT "/dev/ttyACM0"
#define FIFO_PATH "./rtl_output"
#define FRAME_SIZE 24

// Functions to send and receive messages
void sleep_microseconds(long microseconds);
void send_message(int fd, MessageType msg_type, uint8_t flags, uint8_t *payload, uint16_t payload_size);
void set_transceiver_mode(int fd, uint8_t mode);
void set_transceiver_config_register(int fd, uint8_t *register_values, uint16_t size);
void start_sniffing(int fd);
void add_payload_to_fifo(uint8_t *payload, uint16_t payload_size);

#endif
