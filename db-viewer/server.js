const express = require('express');
const path = require('path');
const Database = require('better-sqlite3');
const axios = require('axios');

const app = express();
const PORT = 80;
const DB_PATH = "/database/concentrator.db";

// Servir les fichiers statiques (HTML, JS, etc.)
app.use(express.static(path.join(__dirname, 'public')));

// Ping simple
app.get('/ping', (req, res) => {
  res.send('pong');
});

// Récupération des frames
app.get('/frames', (req, res) => {
  try {
    const db = new Database(DB_PATH, { readonly: true });
    const rows = db
      .prepare("SELECT time, sensor_id, counter, motion, motion2, motion3, motion4, orientation FROM Frames ORDER BY time DESC")
      .all();
    db.close();
    res.json(rows);
  } catch (err) {
    console.error("Database error:", err.message);
    res.status(500).json({ error: "Database access failed" });
  }
});

// Vider la table Frames
app.get('/clear-frames', (req, res) => {
  try {
    const db = new Database(DB_PATH);
    db.prepare("DELETE FROM Frames").run();
    db.close();
    res.send("Frames cleared");
  } catch (err) {
    console.error("Clear error:", err.message);
    res.status(500).send("Failed to clear frames");
  }
});

// Export complet JSON
app.get('/export-json', (req, res) => {
  try {
    const db = new Database(DB_PATH, { readonly: true });
    const rows = db.prepare("SELECT * FROM Frames ORDER BY time DESC").all();
    db.close();

    const jsonData = JSON.stringify(rows, null, 2);
    res.setHeader('Content-Type', 'application/json');
    res.setHeader('Content-Disposition', 'attachment; filename=\"frames.json\"');
    res.send(jsonData);
  } catch (err) {
    console.error("Export error:", err.message);
    res.status(500).send("Export failed");
  }
});

// Renvoi des nuitées à Azure
app.get('/resend-nights-to-azure', async (req, res) => {
  const date = req.query.date;
  if (!date) {
    return res.status(400).send("Missing ?date=YYYY-MM-DD");
  }

  const db = new Database(DB_PATH, { readonly: true });
  const rows = db.prepare(`
    SELECT * FROM Night 
    WHERE date(time) = date(?) 
    ORDER BY time ASC
  `).all(date);
  db.close();

  const endpoint = process.env.AZURE_RELAY_ENDPOINT;
  if (!endpoint) {
    return res.status(500).send("AZURE_RELAY_ENDPOINT not set");
  }

  const sent = [];
  const failed = [];

  for (const row of rows) {
    const payload = {
      type: "night",
      timestamp: row.time,
      sensor: row.sensor_id,
      orientation: row.orientation,
      detected: row.detected
    };

    try {
      await axios.post(endpoint, payload);
      sent.push(row.id);
    } catch (err) {
      failed.push({ id: row.id, error: err.message });
    }
  }

  res.json({ sent, failed });
});

// Démarrage du serveur HTTP
app.listen(PORT, () => {
  console.log(`Smartlease DB viewer running on port ${PORT}`);
});
