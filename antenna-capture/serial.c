#include "serial.h"

// Function to configure the serial port
int configure_serial_port(int fd)
{
    struct termios tty;
    if (tcgetattr(fd, &tty) != 0)
    {
        perror("tcgetattr failed");
        return -1;
    }

    cfsetospeed(&tty, B115200);
    cfsetispeed(&tty, B115200);

    tty.c_cflag = (tty.c_cflag & ~CSIZE) | CS8;
    tty.c_iflag &= ~IGNBRK;
    tty.c_lflag = 0;
    tty.c_oflag = 0;
    tty.c_cc[VMIN] = 1;
    tty.c_cc[VTIME] = 10;

    tty.c_iflag &= ~(IXON | IXOFF | IXANY);
    tty.c_cflag |= (CLOCAL | CREAD);

    if (tcsetattr(fd, TCSANOW, &tty) != 0)
    {
        perror("tcsetattr failed");
        return -1;
    }

    return 0;
}

// Get the number of bytes in the buffer
int get_bytes_in_buffer(int fd)
{
    int bytes_available = 0;

    // Check how many bytes are available for reading
    if (ioctl(fd, FIONREAD, &bytes_available) == -1)
    {
        perror("ioctl error");
        return -1;
    }

    return bytes_available;
}




