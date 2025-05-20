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
document.getElementById("download-json").addEventListener("click", () => {
  window.location.href = "/export-json";
});
document.getElementById("night-page").addEventListener("click", function () {
  window.location.href = "night.html";
});
document.getElementById("truncate-frame").addEventListener("click", function () {
  window.location.href = "/clear-frames";
});
document.getElementById("check-sensors-health").addEventListener("click", () => {
  window.location.href = "/sensor-health-check";
});
