(function () {
    var dataEl = document.getElementById('equipmentStatusChartData');
    var canvas = document.getElementById('equipmentStatusChart');
    if (!dataEl || !canvas || typeof Chart === 'undefined') {
        return;
    }

    var chartData = JSON.parse(dataEl.textContent);
    var total = chartData.values.reduce(function (sum, value) { return sum + value; }, 0);

    function percentLabel(value) {
        if (total === 0) {
            return '0%';
        }
        return Math.round((value / total) * 100) + '%';
    }

    new Chart(canvas, {
        type: 'doughnut',
        data: {
            labels: chartData.labels,
            datasets: [{
                data: chartData.values,
                backgroundColor: chartData.colors,
                borderColor: '#ffffff',
                borderWidth: 2
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            cutout: '60%',
            plugins: {
                legend: {
                    position: 'bottom',
                    labels: {
                        color: '#475569',
                        boxWidth: 12,
                        boxHeight: 12,
                        padding: 12,
                        generateLabels: function (chart) {
                            var dataset = chart.data.datasets[0];
                            return chart.data.labels.map(function (label, i) {
                                var value = dataset.data[i];
                                return {
                                    text: label + ': ' + value + ' (' + percentLabel(value) + ')',
                                    fillStyle: dataset.backgroundColor[i],
                                    strokeStyle: dataset.backgroundColor[i],
                                    index: i
                                };
                            });
                        }
                    }
                },
                tooltip: {
                    callbacks: {
                        label: function (context) {
                            var value = context.parsed;
                            return context.label + ': ' + value + ' (' + percentLabel(value) + ')';
                        }
                    }
                }
            }
        }
    });
})();

(function () {
    var dataEl = document.getElementById('usersByRegionalChartData');
    var canvas = document.getElementById('usersByRegionalChart');
    if (!dataEl || !canvas || typeof Chart === 'undefined') {
        return;
    }

    var chartData = JSON.parse(dataEl.textContent);

    new Chart(canvas, {
        type: 'bar',
        data: {
            labels: chartData.labels,
            datasets: [{
                data: chartData.values,
                backgroundColor: '#2c67e5',
                borderRadius: 4,
                borderSkipped: false,
                barPercentage: 0.6,
                categoryPercentage: 0.8
            }]
        },
        options: {
            indexAxis: 'y',
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: false
                },
                tooltip: {
                    callbacks: {
                        label: function (context) {
                            var value = context.parsed.x;
                            return value + (value === 1 ? ' usuario' : ' usuarios');
                        }
                    }
                }
            },
            scales: {
                x: {
                    beginAtZero: true,
                    ticks: { precision: 0, color: '#64748b' },
                    grid: { color: '#e5e9f0' }
                },
                y: {
                    ticks: { color: '#475569' },
                    grid: { display: false }
                }
            }
        }
    });
})();
