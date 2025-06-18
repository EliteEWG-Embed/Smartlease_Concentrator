#include <stdio.h>
#include <sqlite3.h>
#include "logger.h"

#include "db.h"

int initialize_database(const char *db_path) {
    sqlite3 *db;
    char *err_msg = NULL;
    int rc = sqlite3_open(db_path, &db);

    if (rc != SQLITE_OK) {
        fprintf(stderr, "Cannot open database: %s\n", sqlite3_errmsg(db));
        sqlite3_close(db);
        return rc;
    }

    // Mode WAL pour les accès concurrents
    sqlite3_exec(db, "PRAGMA journal_mode=WAL;", 0, 0, 0);

    const char *sql_create_frames = 
        "CREATE TABLE IF NOT EXISTS Frames ("
        "id INTEGER PRIMARY KEY AUTOINCREMENT,"
        "time TEXT,"
        "sensor_id TEXT,"
        "counter INTEGER,"
        "motion INTEGER,"
        "motion2 INTEGER,"
        "motion3 INTEGER,"
        "motion4 INTEGER,"
        "orientation INTEGER,"
        "payload TEXT"
        ");";

    const char *sql_create_night = 
        "CREATE TABLE IF NOT EXISTS Night ("
        "id INTEGER PRIMARY KEY AUTOINCREMENT,"
        "time TEXT NOT NULL,"
        "sensor_id TEXT NOT NULL,"
        "orientation INTEGER NOT NULL,"
        "detected INTEGER NOT NULL,"
        "sent INTEGER DEFAULT 0"
        ");";

    rc = sqlite3_exec(db, sql_create_frames, 0, 0, &err_msg);
    if (rc != SQLITE_OK) {
        fprintf(stderr, "SQL error creating Frames: %s\n", err_msg);
        sqlite3_free(err_msg);
        sqlite3_close(db);
        return rc;
    }

    rc = sqlite3_exec(db, sql_create_night, 0, 0, &err_msg);
    if (rc != SQLITE_OK) {
        fprintf(stderr, "SQL error creating Night: %s\n", err_msg);
        sqlite3_free(err_msg);
        sqlite3_close(db);
        return rc;
    }

    sqlite3_close(db);
    return SQLITE_OK;
}


int insert_frame(const char *db_path, const char *timestamp, const char *sensor_id, 
                 int counter, int motion, int motion2, int motion3, int motion4, int orientation, const char *payload_hex) {
    sqlite3 *db;
    char *err_msg = NULL;
    int rc = sqlite3_open(db_path, &db);

    if (rc != SQLITE_OK) {
        fprintf(stderr, "Cannot open database: %s\n", sqlite3_errmsg(db));
        return rc;
    }

    char sql[1024];
    snprintf(sql, sizeof(sql),
             "INSERT INTO Frames (time, sensor_id, counter, motion, motion2, motion3, motion4, orientation, payload) "
             "VALUES ('%s', '%s', %d, %d, %d, %d, %d, %d, '%s');",
             timestamp, sensor_id, counter, motion, motion2, motion3, motion4, orientation, payload_hex);

    rc = sqlite3_exec(db, sql, 0, 0, &err_msg);
    if (rc != SQLITE_OK) {
        fprintf(stderr, "SQL error during insert: %s\n", err_msg);
        sqlite3_free(err_msg);
    }

    sqlite3_close(db);
    return rc;
}
