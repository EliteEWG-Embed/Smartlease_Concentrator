$(document).ready(function () {
  $.getJSON("/frames", function (data) {
    const tableData = data.map((row) => [
      row.time,
      row.sensor_id,
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
