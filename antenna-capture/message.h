#ifndef MESSAGE_H
#define MESSAGE_H

#include <stdint.h>
#include <stdlib.h>
#include <ctype.h>
#include <string.h>
#include "serial.h"

#define SOF 0x7F
#define HEADER_SIZE 5
#define MESSAGE_TYPE_START_INDEX 1
#define FLAGS_START_INDEX 2
#define PAYLOAD_SIZE_START_INDEX 3
#define PAYLOAD_START_INDEX 5
#define SNIFF_TIMEOUT_SECONDS 2.0

// Define the start of frame character
typedef enum
{
    UNKNOWN_MSGTYPE = 0x00,
    SET_TRANSCEIVER_CONFIG_REGISTER = 0x01,
    GET_TRANSCEIVER_CONFIG_REGISTER = 0x02,
    GET_TRANSCEIVER_STATUS = 0x03,
    SNIFFER_MESSAGE = 0x04,
    TRANSMIT_DATA_PACKET = 0x05,
    SET_TRANSMISSION_ADDRESS = 0x06,
    GET_TRANSMISSION_ADDRESS = 0x07,
    SET_TRANSCEIVER_MODE = 0x08,
    GET_TRANSCEIVER_MODE = 0x09
} MessageType;

// Define the message types
typedef enum
{
    UNKNOWN = 0,
    START = 1,
    STOP = 2,
    RX_DATA_PACKET = 3
} SnifferMessageType;

// Define the data message types
typedef enum
{
    RX_DATA = 1,
    TX_DATA = 2
} DataMessageType;

// Transceiver modes
typedef enum
{
    POWERDOWN = 1,
    STANDBY = 2,
    RX = 3,
    TX = 4
} TransceiverMode;

typedef enum
{
    NONE = 0,
    REQUEST = 1 << 0,
    RESPONSE = 1 << 1,
    INDICATION = 1 << 2
} Flags;

typedef struct {
    uint8_t message_type;
    uint8_t flags;
    uint16_t payload_size;
    uint8_t *payload;
} Message;

int decode_message(uint8_t *buffer, int len, Message *msg);
void encode_message(MessageType msg_type, uint8_t flags, uint8_t *payload, uint16_t payload_size, uint8_t *encoded_msg);
int remove_rubbish(uint8_t *buffer, int len);
int sniff_next_message(int fd, uint8_t *rx_buffer, int buffer_size, Message *received_msg);
uint16_t get_payload_size(uint8_t *buffer);
int wait_rx_message(int fd, uint8_t *rx_buffer, int buffer_size, double timeout, Message *msg);
void send_message(int fd, MessageType msg_type, uint8_t flags, uint8_t *payload, uint16_t payload_size);

#endif
