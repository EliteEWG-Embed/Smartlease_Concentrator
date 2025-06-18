#include <stdio.h>
#include <string.h>
#include <fcntl.h>
#include <dirent.h>
#include <termios.h>
#include "serial.h"
#include "logger.h"


int try_open_serial(const char *port_path) {
    int fd = open(port_path, O_RDWR | O_NOCTTY | O_SYNC);
    if (fd < 0) {
        return -1;
    }

    if (configure_serial_port(fd) != 0) {
        close(fd);
        return -1;
    }

    return fd;
}

int find_antenna_port(char *valid_port, size_t len) {
    struct dirent *entry;
    DIR *dp = opendir("/dev");
    if (dp == NULL) {
        LOG_ERROR("opendir /dev");
        return -1;
    }

    while ((entry = readdir(dp))) {
        if (strncmp(entry->d_name, "ttyACM", 6) == 0) {
            char path[64];
            snprintf(path, sizeof(path), "/dev/%s", entry->d_name);
            int fd = try_open_serial(path);
            if (fd >= 0) {
                strncpy(valid_port, path, len);
                close(fd);
                closedir(dp);
                return 0;
            }
        }
    }

    closedir(dp);
    return -1;
}