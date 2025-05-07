const express = require('express');
const path = require('path');
const Database = require('better-sqlite3');

const app = express();
const PORT = 80;
const DB_PATH = "/database/concentrator.db";

app.use(express.static(path.join(__dirname, 'public')));

app.get('/frames', (req, res) => {
    try {
        const db = new Database(DB_PATH, { readonly: true });
        const rows = db
            .prepare("SELECT time, sensor_id, counter, motion, orientation FROM Frames ORDER BY time DESC")
            .all();
        db.close();
        res.json(rows);
    } catch (err) {
        console.error("Database error:", err.message);
        res.status(500).json({ error: "Database access failed" });
    }
});

app.listen(PORT, () => {
    console.log(`Smartlease DB viewer running on port ${PORT}`);
});
