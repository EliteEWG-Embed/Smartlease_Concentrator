#include "message.h"

// Decodes a message from the buffer
int decode_message(uint8_t *buffer, int len, Message *msg)
{
    if (len < HEADER_SIZE)
        return -1;
    len = remove_rubbish(buffer, len);

    // Check if buffer has a valid message
    if (len >= HEADER_SIZE)
    {
        uint16_t payload_size = get_payload_size(buffer);
        int message_size = HEADER_SIZE + payload_size;

        // Check if we have the full message
        if (len >= message_size)
        {
            msg->message_type = buffer[1];
            msg->flags = buffer[2];
            msg->payload_size = payload_size;
            msg->payload = malloc(payload_size);
            memcpy(msg->payload, &buffer[5], payload_size);

            // Remove the message from the buffer
            memmove(buffer, &buffer[message_size], len - message_size);
            return message_size;
        }
    }

    return -1;
}

// Encodes a message
void encode_message(MessageType msg_type, uint8_t flags, uint8_t *payload, uint16_t payload_size, uint8_t *encoded_msg)
{
    // Header
    encoded_msg[0] = SOF;  // Start of Frame
    encoded_msg[1] = msg_type;
    encoded_msg[2] = flags;
    encoded_msg[3] = (payload_size >> 8) & 0xFF;  // MSB of payload size
    encoded_msg[4] = payload_size & 0xFF;         // LSB of payload size

    // Payload: Copy the payload to the encoded message
    for (int i = 0; i < payload_size; i++) {
        if (isprint(payload[i])) {
            encoded_msg[5 + i] = payload[i];  // ASCII value
        } else {
            encoded_msg[5 + i] = payload[i];  // Hex value
        }
    }
}

// Helper function to get the payload size
uint16_t get_payload_size(uint8_t *buffer)
{
    return (buffer[3] << 8) | buffer[4];
}

// Function to remove rubbish from the buffer
int remove_rubbish(uint8_t *buffer, int len)
{
    int index = -1;
    // Find SOF in buffer
    for (int i = 0; i < len; i++)
    {
        if (buffer[i] == SOF)
        {
            index = i;
            break;
        }
    }
    if (index > 0)
    {
        // Remove rubbish before the SOF
        memmove(buffer, &buffer[index], len - index);
        return len - index;
    }
    else if (index == -1)
    {
        // No SOF found, discard all data
        return 0;
    }
    return len;
}

// Sniffs the next message
int sniff_next_message(int fd, uint8_t *rx_buffer, int buffer_size, Message *received_msg)
{
    int success = wait_rx_message(fd, rx_buffer, buffer_size, SNIFF_TIMEOUT_SECONDS, received_msg); // 2-second timeout
    if (success)
    {
        if (received_msg->message_type == SNIFFER_MESSAGE)
        {
            return 1; // Successfully sniffed a data packet
        }
    }
    return 0;
}

// Waits for a message from the serial port
int wait_rx_message(int fd, uint8_t *rx_buffer, int buffer_size, double timeout, Message *msg)
{
    fd_set read_fds;
    struct timeval tv;
    tv.tv_sec = (int)timeout;
    tv.tv_usec = (timeout - (int)timeout) * 1000000;

    FD_ZERO(&read_fds);
    FD_SET(fd, &read_fds);

    int result = select(fd + 1, &read_fds, NULL, NULL, &tv);
    if (result > 0)
    {
        int n = read(fd, rx_buffer, buffer_size);
        if (n > 0)
        {
            int decoded_len = decode_message(rx_buffer, n, msg);
            if (decoded_len > 0)
            {
                return 1; // Success
            }
        }
    }
    return 0; // Timeout or no message
}

// Function to write a message to the transceiver
void send_message(int fd, MessageType msg_type, uint8_t flags, uint8_t *payload, uint16_t payload_size)
{
    uint8_t encoded_msg[HEADER_SIZE + payload_size];
    encode_message(msg_type, flags, payload, payload_size, encoded_msg);
    int encoded_size = HEADER_SIZE + payload_size;
    int success = write(fd, encoded_msg, encoded_size);
    if (success == -1)
    {
        perror("write failed");
    }
}
