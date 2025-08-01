const express = require('express');
const path = require('path');
const Database = require('better-sqlite3');

const app = express();
const PORT = 80;
const DB_PATH = "/database/concentrator.db";


// Fonction utilitaire : hex vers ID décimal formaté
function convertSensorName(sensorId) {
  if (!sensorId || sensorId.length !== 8) return sensorId;
  try {
    const byte1 = parseInt(sensorId.slice(0, 2), 16);
    const byte2 = parseInt(sensorId.slice(2, 4), 16);
    const byte3 = parseInt(sensorId.slice(4, 6), 16);
    const byte4 = parseInt(sensorId.slice(6, 8), 16);
    return (
      String(byte1).padStart(3, '0') +
      String(byte2).padStart(3, '0') +
      String(byte3).padStart(3, '0') +
      String(byte4).padStart(3, '0')
    );
  } catch (e) {
    return sensorId;
  }
}

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

// Vider la table Night
app.get('/clear-night', (req, res) => {
  try {
    const db = new Database(DB_PATH);
    db.prepare("DELETE FROM Night").run();
    db.close();
    res.send("Nights cleared");
  } catch (err) {
    console.error("Clear error:", err.message);
    res.status(500).send("Failed to clear nights");
  }
});

// Export complet JSON frames
app.get('/export-frame', (req, res) => {
  try {
    const db = new Database(DB_PATH, { readonly: true });
    const rows = db.prepare("SELECT * FROM Frames ORDER BY time DESC").all();
    db.close();

    // Ajout de sensor_id_dec
    const enhancedRows = rows.map(row => ({
      ...row,
      sensor_id_dec: convertSensorName(row.sensor_id),
    }));

    const jsonData = JSON.stringify(enhancedRows, null, 2);
    res.setHeader('Content-Type', 'application/json');
    res.setHeader('Content-Disposition', 'attachment; filename="frames.json"');
    res.send(jsonData);
  } catch (err) {
    console.error("Export error:", err.message);
    res.status(500).send("Export failed");
  }
});

// Export complet JSON night
app.get('/export-night', (req, res) => {
  try {
    const db = new Database(DB_PATH, { readonly: true });
    const rows = db.prepare("SELECT * FROM Night ORDER BY time DESC").all();
    db.close();

    const jsonData = JSON.stringify(rows, null, 2);
    res.setHeader('Content-Type', 'application/json');
    res.setHeader('Content-Disposition', 'attachment; filename=\"nights.json\"');
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
  

  const db = new Database(DB_PATH, { readonly: false });
  try {
    const rows = db.prepare(`
      UPDATE Night SET sent = 0
      WHERE date(time) = ?
    `).run(date);
    db.close();
    if (rows.changes === 0) {
      return res.status(404).send("No nights found for the specified date");
    }
    return res.status(200).send(`Nights for ${date} marked as unsent`);
  } catch (err) {
    console.error("Resend error:", err.message);
    db.close();
    return res.status(500).send("Failed to mark nights as unsent");
  }
});

app.get('/nights', (req, res) => {
  try {
    const db = new Database(DB_PATH, { readonly: true });
    const rows = db
      .prepare("SELECT id, time, sensor_id, orientation, detected, sent FROM Night ORDER BY time DESC")
      .all();
    db.close();
    res.json(rows);
  } catch (err) {
    console.error("Database error:", err.message);
    res.status(500).json({ error: "Database access failed" });
  } 
});


// Démarrage du serveur HTTP
app.listen(PORT, () => {
  console.log(`Smartlease DB viewer running on port ${PORT}`);
});
