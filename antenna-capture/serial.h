#ifndef SERIAL_H
#define SERIAL_H

#include <termios.h>
#include <sys/ioctl.h>
#include <stdio.h>
#include <stdint.h>
#include <sys/select.h>
#include <unistd.h>

// Déclaration des fonctions pour la configuration série
int configure_serial_port(int fd);
int get_bytes_in_buffer(int fd);

#endif
