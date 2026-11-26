(function () {
    var dataEl = document.getElementById('casoTIBarChartData');
    var canvas = document.getElementById('casoTIBarChart');
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
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: false },
                tooltip: {
                    callbacks: {
                        label: function (context) {
                            var value = context.parsed.y;
                            return value + (value === 1 ? ' caso' : ' casos');
                        }
                    }
                }
            },
            scales: {
                x: {
                    ticks: { color: '#475569' },
                    grid: { display: false }
                },
                y: {
                    beginAtZero: true,
                    ticks: { precision: 0, color: '#64748b' },
                    grid: { color: '#e5e9f0' }
                }
            }
        }
    });
})();

(function () {
    var dataEl = document.getElementById('casoTIPieChartData');
    var canvas = document.getElementById('casoTIPieChart');
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
        type: 'pie',
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
                                var avg = chartData.avgResolution && chartData.avgResolution[i];
                                var suffix = avg ? ' — ' + avg : '';
                                return {
                                    text: label + ': ' + value + ' (' + percentLabel(value) + ')' + suffix,
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
                            var avg = chartData.avgResolution && chartData.avgResolution[context.dataIndex];
                            var suffix = avg ? ' — tiempo promedio: ' + avg : '';
                            return context.label + ': ' + value + ' (' + percentLabel(value) + ')' + suffix;
                        }
                    }
                }
            }
        }
    });
})();

(function () {
    var dataEl = document.getElementById('casoTIStackedChartData');
    var canvas = document.getElementById('casoTIStackedChart');
    if (!dataEl || !canvas || typeof Chart === 'undefined') {
        return;
    }

    var chartData = JSON.parse(dataEl.textContent);

    new Chart(canvas, {
        type: 'bar',
        data: {
            labels: chartData.labels,
            datasets: chartData.series.map(function (serie) {
                return {
                    label: serie.label,
                    data: serie.values,
                    backgroundColor: serie.color,
                    borderRadius: 4,
                    borderSkipped: false
                };
            })
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    position: 'bottom',
                    labels: { color: '#475569', boxWidth: 12, boxHeight: 12, padding: 12 }
                },
                tooltip: {
                    callbacks: {
                        label: function (context) {
                            var value = context.parsed.y;
                            return context.dataset.label + ': ' + value + (value === 1 ? ' caso' : ' casos');
                        }
                    }
                }
            },
            scales: {
                x: {
                    stacked: true,
                    ticks: { color: '#475569' },
                    grid: { display: false }
                },
                y: {
                    stacked: true,
                    beginAtZero: true,
                    ticks: { precision: 0, color: '#64748b' },
                    grid: { color: '#e5e9f0' }
                }
            }
        }
    });
})();

(function () {
    var dataEl = document.getElementById('casoTITiempoPromedioChartData');
    var canvas = document.getElementById('casoTITiempoPromedioChart');
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
                backgroundColor: '#16a34a',
                borderRadius: 4,
                borderSkipped: false,
                barPercentage: 0.6,
                categoryPercentage: 0.8
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: false },
                tooltip: {
                    callbacks: {
                        label: function (context) {
                            var label = chartData.tooltipLabels && chartData.tooltipLabels[context.dataIndex];
                            return 'Tiempo promedio: ' + (label || (context.parsed.y + ' min'));
                        }
                    }
                }
            },
            scales: {
                x: {
                    ticks: { color: '#475569' },
                    grid: { display: false }
                },
                y: {
                    beginAtZero: true,
                    ticks: { precision: 0, color: '#64748b' },
                    grid: { color: '#e5e9f0' },
                    title: { display: true, text: 'Minutos', color: '#64748b' }
                }
            }
        }
    });
})();
