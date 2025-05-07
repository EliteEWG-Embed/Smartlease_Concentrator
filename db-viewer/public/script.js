$(document).ready(function () {
  $.getJSON("/frames", function (data) {
    const tableData = data.map(row => [
      row.time,
      row.sensor_id,
      row.counter,
      row.motion,
      row.orientation
    ]);

    $('#data-table').DataTable({
      data: tableData,
      columns: [
        { title: "Heure" },
        { title: "ID Capteur" },
        { title: "Compteur" },
        { title: "Mouvement" },
        { title: "Orientation" }
      ]
    });
  });
});