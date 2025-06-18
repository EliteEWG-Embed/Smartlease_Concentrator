#include <stdio.h>
#include <stdlib.h>
#include <stdarg.h>
#include <string.h>
#include <time.h>
#include <sys/stat.h>
#include <unistd.h>
#include <dirent.h>

#define LOG_DIR "/logs"
#define MAX_LOG_DAYS 7

static FILE *log_file = NULL;
int log_console_enabled = 0;


void cleanup_old_logs() {
    DIR *dir = opendir(LOG_DIR);
    if (!dir) return;

    time_t now = time(NULL);
    struct dirent *entry;
    while ((entry = readdir(dir)) != NULL) {
        if (strncmp(entry->d_name, "antenna.", 8) != 0) continue;

        char filepath[256];
        snprintf(filepath, sizeof(filepath), "%s/%s", LOG_DIR, entry->d_name);

        struct stat st;
        if (stat(filepath, &st) == 0) {
            double age_days = difftime(now, st.st_mtime) / (60 * 60 * 24);
            if (age_days > MAX_LOG_DAYS) {
                remove(filepath);
            }
        }
    }
    closedir(dir);
}

void init_logger() {
    mkdir(LOG_DIR, 0755);

    char *env = getenv("LOG_CONSOLE_ENABLED");
    if (env && strcmp(env, "true") == 0) return;

    cleanup_old_logs();

    time_t now = time(NULL);
    struct tm *tm_info = localtime(&now);
    char filename[256];
    strftime(filename, sizeof(filename), LOG_DIR"/antenna.%Y-%m-%d.log", tm_info);
    log_file = fopen(filename, "a");
}

void log_message(const char *level, const char *format, va_list args) {
    time_t now = time(NULL);
    struct tm *tm_info = localtime(&now);

    char time_buf[20];
    strftime(time_buf, sizeof(time_buf), "%Y-%m-%d %H:%M:%S", tm_info);

    if (getenv("LOG_CONSOLE_ENABLED") && strcmp(getenv("LOG_CONSOLE_ENABLED"), "true") == 0) {
        fprintf(stderr, "[%s] %s: ", level, time_buf);
        vfprintf(stderr, format, args);
        fprintf(stderr, "\n");
    } else if (log_file) {
        fprintf(log_file, "[%s] %s: ", level, time_buf);
        vfprintf(log_file, format, args);
        fprintf(log_file, "\n");
        fflush(log_file);
    }
}

void log_info(const char *format, ...) {
    va_list args;
    va_start(args, format);
    log_message("INFO", format, args);
    va_end(args);
}

void log_error(const char *format, ...) {
    va_list args;
    va_start(args, format);
    log_message("ERROR", format, args);
    va_end(args);
}
