// Permite agregar un Cargo nuevo desde los formularios de Usuario sin salir de la pantalla.
// Solo se activa dentro de un contenedor [data-cargo-quick-add] (el botón "+" solo se
// renderiza para Administrador — ver Views/UserAdmin/Create.cshtml y Edit.cshtml).
(function () {
    document.querySelectorAll('[data-cargo-quick-add]').forEach(function (container) {
        var toggleBtn = container.querySelector('[data-cargo-toggle]');
        var formDiv = container.querySelector('[data-cargo-form]');
        var input = container.querySelector('[data-cargo-input]');
        var submitBtn = container.querySelector('[data-cargo-submit]');
        var cancelBtn = container.querySelector('[data-cargo-cancel]');
        var errorDiv = container.querySelector('[data-cargo-error]');
        var select = container.querySelector('[data-cargo-select]');

        if (!toggleBtn || !select) {
            return;
        }

        toggleBtn.addEventListener('click', function () {
            formDiv.classList.toggle('d-none');
            if (!formDiv.classList.contains('d-none')) {
                input.focus();
            }
        });

        cancelBtn.addEventListener('click', function () {
            formDiv.classList.add('d-none');
            input.value = '';
            errorDiv.classList.add('d-none');
        });

        submitBtn.addEventListener('click', function () {
            var name = input.value.trim();
            if (!name) {
                return;
            }

            var tokenInput = container.closest('form').querySelector('input[name="__RequestVerificationToken"]');
            var body = new URLSearchParams();
            body.append('name', name);
            body.append('__RequestVerificationToken', tokenInput.value);

            fetch('/Cargo/QuickCreate', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: body.toString()
            }).then(function (response) {
                if (!response.ok) {
                    return response.text().then(function (text) {
                        throw new Error(text || 'No se pudo agregar el cargo.');
                    });
                }
                return response.json();
            }).then(function (data) {
                var option = document.createElement('option');
                option.value = data.id;
                option.textContent = data.name;
                select.appendChild(option);
                select.value = data.id;
                formDiv.classList.add('d-none');
                input.value = '';
                errorDiv.classList.add('d-none');
            }).catch(function (err) {
                errorDiv.textContent = err.message;
                errorDiv.classList.remove('d-none');
            });
        });
    });
})();
