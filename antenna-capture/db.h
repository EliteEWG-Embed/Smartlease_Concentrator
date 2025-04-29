#ifndef DB_H
#define DB_H

int initialize_database(const char *db_path);
int insert_frame(const char *db_path, const char *timestamp, const char *sensor_id, 
                 int counter, int count_value, int motion, int orientation, const char *payload_hex);

#endif // DB_H
