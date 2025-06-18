#include "capture.h"
#include "logger.h"

// sleep function
void sleep_microseconds(long microseconds) {
    struct timespec ts;
    ts.tv_sec = microseconds / 1000000;
    ts.tv_nsec = (microseconds % 1000000) * 1000;
    nanosleep(&ts, NULL);
}

// Set transceiver mode
void set_transceiver_mode(int fd, uint8_t mode)
{
    uint8_t payload[1] = {mode};
    send_message(fd, SET_TRANSCEIVER_MODE, REQUEST, payload, sizeof(payload));
}

// Set transceiver configuration
void set_transceiver_config_register(int fd, uint8_t *register_values, uint16_t size)
{
    send_message(fd, SET_TRANSCEIVER_CONFIG_REGISTER, REQUEST, register_values, size);
}

// Starts the sniffing process
void start_sniffing(int fd)
{
    uint8_t payload[1] = {START};
    send_message(fd, SNIFFER_MESSAGE, REQUEST, payload, sizeof(payload));
}

// Stops the sniffing process
void stop_sniffing(int fd)
{
    uint8_t payload[1] = {STOP};
    send_message(fd, SNIFFER_MESSAGE, REQUEST, payload, sizeof(payload));
}

// Add payload to the FIFO
void add_payload_to_fifo(uint8_t *payload, uint16_t payload_size)
{
    FILE *fifo = fopen(FIFO_PATH, "w");
    if (fifo == NULL)
    {
        LOG_ERROR("Unable to open FIFO");
        return;
    }

    // Get the current timestamp
    time_t rawtime;
    struct tm *timeinfo;
    char time_buffer[20]; // Format: YYYY-MM-DD HH:MM:SS
    time(&rawtime);
    timeinfo = localtime(&rawtime);
    strftime(time_buffer, sizeof(time_buffer), "%Y-%m-%d %H:%M:%S", timeinfo);

    // Hexadecimal representation of the payload
    char hex_payload[payload_size * 2 + 1]; // 2 characters per byte
    for (uint16_t i = 0; i < payload_size; i++)
    {
        sprintf(&hex_payload[i * 2], "%02X", payload[i]);
    }
    // Get the length of the payload in bits
    uint16_t payload_length_bits = payload_size * 8;

    // JSON format: {"time" : "YYYY-MM-DD HH:MM:SS", "len" : payload_length_bits, "data" : "hex_payload"}
    if (fprintf(fifo, "{\"time\" : \"%s\", \"len\" : %d, \"data\" : \"%s\"}\n", 
                time_buffer, payload_length_bits, hex_payload) < 0)
    {
        LOG_ERROR("Unable to write to FIFO");
    }
    
    fclose(fifo);
}

// Add payload to the log file
void add_payload_to_log(uint8_t *payload, uint16_t payload_size, const char *log_path)
{
    if (log_path == NULL) {
        return;
    }

    FILE *log_file = fopen(log_path, "a"); // Open in append mode
    if (log_file == NULL)
    {
        LOG_ERROR("Unable to open log file");
        return;
    }

    // Get the current timestamp
    time_t rawtime;
    struct tm *timeinfo;
    char time_buffer[20]; // Format: YYYY-MM-DD HH:MM:SS
    time(&rawtime);
    timeinfo = localtime(&rawtime);
    strftime(time_buffer, sizeof(time_buffer), "%Y-%m-%d %H:%M:%S", timeinfo);

    // Hexadecimal representation of the payload
    char hex_payload[payload_size * 2 + 1]; // 2 characters per byte
    for (uint16_t i = 0; i < payload_size; i++)
    {
        sprintf(&hex_payload[i * 2], "%02X", payload[i]);
    }
    // Get the length of the payload in bits
    uint16_t payload_length_bits = payload_size * 8;

    // Write to the log file in JSON format
    if (fprintf(log_file, "{\"time\" : \"%s\", \"len\" : %d, \"data\" : \"%s\"}\n", 
                time_buffer, payload_length_bits, hex_payload) < 0)
    {
        LOG_ERROR("Unable to write to log file");
    }
    
    fclose(log_file);
}