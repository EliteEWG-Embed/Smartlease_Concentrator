# Variables
CC = gcc
CFLAGS = -Wall -Wextra -O2 -fPIE
LDFLAGS = -lsqlite3 -pie
SRC = main.c capture.c serial.c message.c db.c
OBJ = $(SRC:.c=.o)
TARGET = antenna_capture

# Règles
all: $(TARGET)

$(TARGET): $(OBJ)
	$(CC) $(CFLAGS) -o $@ $^ $(LDFLAGS)

%.o: %.c
	$(CC) $(CFLAGS) -c $<

clean:
	rm -f $(OBJ) $(TARGET)

re: clean all
