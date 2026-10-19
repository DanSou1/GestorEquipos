// Captura de firma a mano alzada en <canvas>, sin librería externa (ver nota de diseño en el
// plan: coherente con el único patrón AJAX a medida ya existente, cargo-quick-add.js).
// Formato de salida: JSON de trazos — cada trazo es un array de puntos {x,y} normalizados al
// tamaño lógico del canvas (CANVAS_WIDTH x CANVAS_HEIGHT), para poder escalar de forma
// determinista al renderizar la firma dentro del PDF (ver MaintenancePdfService).
(function () {
    const CANVAS_WIDTH = 600;
    const CANVAS_HEIGHT = 200;

    function setupDrawableCanvas(canvas) {
        canvas.width = CANVAS_WIDTH;
        canvas.height = CANVAS_HEIGHT;
        const ctx = canvas.getContext('2d');
        ctx.lineWidth = 3;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        ctx.strokeStyle = '#000';

        let strokes = [];
        let currentStroke = null;
        let drawing = false;

        function toCanvasPoint(evt) {
            const rect = canvas.getBoundingClientRect();
            const scaleX = CANVAS_WIDTH / rect.width;
            const scaleY = CANVAS_HEIGHT / rect.height;
            return {
                x: (evt.clientX - rect.left) * scaleX,
                y: (evt.clientY - rect.top) * scaleY
            };
        }

        function redraw() {
            ctx.clearRect(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);
            for (const stroke of strokes) {
                if (stroke.length < 2) continue;
                ctx.beginPath();
                ctx.moveTo(stroke[0].x, stroke[0].y);
                for (let i = 1; i < stroke.length; i++) {
                    ctx.lineTo(stroke[i].x, stroke[i].y);
                }
                ctx.stroke();
            }
        }

        canvas.addEventListener('pointerdown', function (evt) {
            drawing = true;
            currentStroke = [toCanvasPoint(evt)];
            strokes.push(currentStroke);
            canvas.setPointerCapture(evt.pointerId);
            evt.preventDefault();
        });

        canvas.addEventListener('pointermove', function (evt) {
            if (!drawing || !currentStroke) return;
            currentStroke.push(toCanvasPoint(evt));
            redraw();
            evt.preventDefault();
        });

        function endStroke(evt) {
            if (!drawing) return;
            drawing = false;
            currentStroke = null;
            evt.preventDefault();
        }

        canvas.addEventListener('pointerup', endStroke);
        canvas.addEventListener('pointercancel', endStroke);
        canvas.addEventListener('pointerleave', endStroke);

        return {
            clear: function () {
                strokes = [];
                redraw();
            },
            isEmpty: function () {
                return strokes.length === 0;
            },
            toJson: function () {
                return JSON.stringify(strokes);
            }
        };
    }

    function drawReadOnly(canvas, strokesJson) {
        canvas.width = CANVAS_WIDTH;
        canvas.height = CANVAS_HEIGHT;
        const ctx = canvas.getContext('2d');
        ctx.lineWidth = 3;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        ctx.strokeStyle = '#000';

        let strokes = [];
        try {
            strokes = JSON.parse(strokesJson || '[]');
        } catch (e) {
            strokes = [];
        }

        ctx.clearRect(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);
        for (const stroke of strokes) {
            if (stroke.length < 2) continue;
            ctx.beginPath();
            ctx.moveTo(stroke[0].x, stroke[0].y);
            for (let i = 1; i < stroke.length; i++) {
                ctx.lineTo(stroke[i].x, stroke[i].y);
            }
            ctx.stroke();
        }
    }

    window.SignatureCapture = {
        setupDrawableCanvas: setupDrawableCanvas,
        drawReadOnly: drawReadOnly
    };
})();
