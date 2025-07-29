#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <string.h>
#include <fcntl.h>
#include <unistd.h>
#include <time.h>
#include <signal.h>
#include "capture.h"
#include "serial.h"
#include "message.h"
#include "db.h"  
#include <errno.h>
#include "port_scanner.h"
#include "logger.h"


#define DB_PATH "/database/concentrator.db"
#define ORIENTATION_THRESHOLD 0x01
#define SLEEP_DURATION_MICROSECONDS 1000000 // 1 seconde

volatile sig_atomic_t stop = 0;

void handle_sigint(int sig) {
    stop = 1;
}

void hex_to_ascii(uint8_t *src, char *dest, int len) {
    for (int i = 0; i < len; i++) {
        sprintf(dest + i * 2, "%02X", src[i]);
    }
    dest[len * 2] = '\0';
}

int parse_and_store(uint8_t *payload, size_t payload_size) {
    if (payload_size < 13) {
        return -1;
    }

    char time_buffer[20];
    time_t now = time(NULL);
    strftime(time_buffer, sizeof(time_buffer), "%Y-%m-%d %H:%M:%S", localtime(&now));

    // Vérifier entête
    //if (!(payload[0] == 0x03 && payload[1] == 0x0B)) return -1;

    // ID capteur
    char sensor_id[9];
    snprintf(sensor_id, sizeof(sensor_id), "%02X%02X%02X%02X",
             payload[2], payload[3], payload[4], payload[5]);

    // Check if Sensor ID is valid (should start with 0xA0, 0xE6 or 0xE9)
    if (sensor_id[0] != 'A' && sensor_id[0] != 'E') {
        LOG_ERROR("Invalid Sensor ID: %s", sensor_id);
        return -1;
    }
    

    // Compteur
    uint8_t counter = payload[6];

    // Valeur de mouvement
    int motion4 = payload[10];
    int motion3 = payload[9];
    int motion2 = payload[8];
    int motion = payload[7];

    // Orientation
    uint8_t orientation_raw = payload[11];
    uint8_t orientation = (orientation_raw & ORIENTATION_THRESHOLD) > 0 ? 1 : 0;

    // Payload complet en hex
    char payload_hex[payload_size * 2 + 1];
    hex_to_ascii(payload, payload_hex, payload_size);

    return insert_frame(DB_PATH, time_buffer, sensor_id, counter, motion, motion2, motion3, motion4, orientation, payload_hex);
}

int main(int argc, char *argv[]) {
    init_logger();
    if (argc < 2) {
        fprintf(stderr, "Usage: %s <serial_port>\n", argv[0]);
        return 1;
    }

    const char *serial_port = argv[1];
    int fd = open(serial_port, O_RDWR | O_NOCTTY | O_SYNC);
    if (fd < 0) {
        LOG_ERROR("Unable to open serial port");
        return 1;
    }

    if (configure_serial_port(fd) != 0) {
        LOG_ERROR("Serial config error");
        close(fd);
        return 1;
    }

    printf("Connected to gateway via %s\n", serial_port);

    initialize_database(DB_PATH);

    signal(SIGINT, handle_sigint);

    // Init du transceiver
    set_transceiver_mode(fd, STANDBY);
    usleep(SLEEP_DURATION_MICROSECONDS);

    uint8_t rx_register_values[] = RX_REGISTER_VALUES;
    set_transceiver_config_register(fd, rx_register_values, sizeof(rx_register_values));
    usleep(SLEEP_DURATION_MICROSECONDS);

    set_transceiver_mode(fd, RX);
    usleep(SLEEP_DURATION_MICROSECONDS);

    start_sniffing(fd);
    usleep(SLEEP_DURATION_MICROSECONDS);

    printf("Sniffing started...\n");

    while (!stop) {
        uint16_t buffer_size = get_bytes_in_buffer(fd);
        if (buffer_size == 0) {
            usleep(100000);
            continue;
        }

        uint8_t rx_buffer[buffer_size];
        Message received_msg;
        if (sniff_next_message(fd, rx_buffer, buffer_size, &received_msg)) {
            /*LOG_INFO("Frame received: ");
            for (int i = 0; i < received_msg.payload_size; i++) {
                //printf("%02X", received_msg.payload[i]);
            }
            //printf("\n");    */
            
            
            parse_and_store(received_msg.payload, received_msg.payload_size);
        } else if (errno == EIO) {
            printf("Port série déconnecté. Tentative de reconnexion...\n");
            close(fd);

            // Attente du retour de l'antenne
            sleep(1);
            while (find_antenna_port(serial_port, sizeof(serial_port)) != 0) {
                printf("Antenne non détectée. Nouvelle tentative dans 1s...\n");
                sleep(1);
            }

            fd = open(serial_port, O_RDWR | O_NOCTTY | O_SYNC);
            if (fd < 0 || configure_serial_port(fd) != 0) {
                LOG_ERROR("Reconnexion échouée");
                continue;
            }

            printf("Antenne reconnectée sur %s\n", serial_port);

            set_transceiver_mode(fd, STANDBY);
            usleep(SLEEP_DURATION_MICROSECONDS);
            uint8_t rx_register_values[] = RX_REGISTER_VALUES;
            set_transceiver_config_register(fd, rx_register_values, sizeof(rx_register_values));
            usleep(SLEEP_DURATION_MICROSECONDS);
            set_transceiver_mode(fd, RX);
            usleep(SLEEP_DURATION_MICROSECONDS);
            start_sniffing(fd);
            usleep(SLEEP_DURATION_MICROSECONDS);

        }
    }

    //stop_sniffing(fd);
    close(fd);

    printf("Capture stopped. Exiting cleanly.\n");
    return 0;
}