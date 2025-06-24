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


$(document).ready(function () {
  $.getJSON("/frames", function (data) {
    const tableData = data.map((row) => [
      row.time,
      convertSensorName(row.sensor_id), 
      row.counter,
      row.motion,
      row.motion2,
      row.motion3,
      row.motion4,
      row.orientation,
    ]);

    $("#data-table").DataTable({
      data: tableData,
      columns: [
        { title: "Heure" },
        { title: "ID Capteur" },
        { title: "Compteur" },
        { title: "Mouvement" },
        { title: "Mouvement2" },
        { title: "Mouvement3" },
        { title: "Mouvement4" },
        { title: "Orientation" },
      ],
    });
  });
});

$(document).ready(function () {
  $.getJSON("/nights", function (data) {
    const tableData = data.map((row) => [
      row.time,
      convertSensorName(row.sensor_id),
      row.orientation,
      row.detected ? "Oui" : "Non",
      row.sent ? "Oui" : "Non",
    ]);
    $("#nights-table").DataTable({
      data: tableData,
      columns: [
        { title: "Heure" },
        { title: "ID Capteur" },
        { title: "Orientation" },
        { title: "Détecté" },
        { title: "Envoyé" },
      ],
    });
  });
});


function bindClick(id, url) {
  const el = document.getElementById(id);
  if (el) {
    el.addEventListener("click", () => {
      window.location.href = url;
    });
  }
}

bindClick("download-night", "/export-night");
bindClick("download-frame", "/export-frame");
bindClick("night-page", "night.html");
bindClick("frame-page", "frame.html");
bindClick("index-page", "index.html");
bindClick("truncate-frame", "/clear-frames");