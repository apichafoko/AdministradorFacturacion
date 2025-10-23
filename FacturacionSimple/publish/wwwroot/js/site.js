// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Funcionalidad para mostrar indicador de carga durante procesamiento de archivos grandes
(function () {
    'use strict';

    // Función para formatear bytes a formato legible
    function formatBytes(bytes, decimals = 2) {
        if (bytes === 0) return '0 Bytes';
        const k = 1024;
        const dm = decimals < 0 ? 0 : decimals;
        const sizes = ['Bytes', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
    }

    // Inicializar el overlay de carga cuando el DOM esté listo
    document.addEventListener('DOMContentLoaded', function () {
        console.log('Site.js cargado correctamente');

        // Buscar el formulario de subida de archivos por ID
        const uploadForm = document.getElementById('uploadForm');
        const fileInput = uploadForm ? uploadForm.querySelector('input[type="file"]') : null;

        console.log('Formulario encontrado:', uploadForm);
        console.log('Input de archivo encontrado:', fileInput);

        if (uploadForm && fileInput) {
            uploadForm.addEventListener('submit', function (e) {
                const file = fileInput.files[0];

                console.log('Formulario enviado, archivo:', file);

                if (file) {
                    // Mostrar overlay de carga
                    showLoadingOverlay(file);
                }
            });
        } else {
            console.error('No se encontró el formulario de subida');
        }
    });

    function showLoadingOverlay(file) {
        // Crear el overlay si no existe
        let overlay = document.getElementById('loading-overlay');

        if (!overlay) {
            overlay = document.createElement('div');
            overlay.id = 'loading-overlay';
            overlay.className = 'loading-overlay';
            overlay.innerHTML = `
                <div class="loading-content">
                    <div class="spinner"></div>
                    <h4>Procesando archivo...</h4>
                    <div class="progress-bar-container">
                        <div class="progress-bar" id="progress-bar">
                            <span id="progress-text">0%</span>
                        </div>
                    </div>
                    <p class="loading-text">Por favor espere mientras procesamos su archivo.</p>
                    <p class="file-size-info">Archivo: ${file.name} (${formatBytes(file.size)})</p>
                    <p class="loading-text" id="status-message">Subiendo archivo...</p>
                </div>
            `;
            document.body.appendChild(overlay);
        }

        // Mostrar el overlay
        overlay.classList.add('active');

        // Simular progreso de subida (0-30%)
        simulateUploadProgress();
    }

    function simulateUploadProgress() {
        let progress = 0;
        const progressBar = document.getElementById('progress-bar');
        const progressText = document.getElementById('progress-text');
        const statusMessage = document.getElementById('status-message');

        const uploadInterval = setInterval(function () {
            progress += Math.random() * 10;
            if (progress > 30) {
                progress = 30;
                clearInterval(uploadInterval);
                statusMessage.textContent = 'Procesando datos del archivo...';

                // Simular progreso de procesamiento (30-90%)
                simulateProcessingProgress();
            }
            updateProgress(progress);
        }, 200);
    }

    function simulateProcessingProgress() {
        let progress = 30;
        const statusMessage = document.getElementById('status-message');

        const processingInterval = setInterval(function () {
            progress += Math.random() * 5;
            if (progress > 90) {
                progress = 90;
                clearInterval(processingInterval);
                statusMessage.textContent = 'Finalizando...';
            }
            updateProgress(progress);
        }, 500);
    }

    function updateProgress(percent) {
        const progressBar = document.getElementById('progress-bar');
        const progressText = document.getElementById('progress-text');

        if (progressBar && progressText) {
            progressBar.style.width = percent + '%';
            progressText.textContent = Math.round(percent) + '%';
        }
    }

    // Conectar con SignalR para recibir actualizaciones de progreso en tiempo real
    // (Si SignalR está configurado)
    if (typeof signalR !== 'undefined') {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/progressHub")
            .build();

        connection.on("UpdateProgress", function (progress) {
            updateProgress(progress);

            const statusMessage = document.getElementById('status-message');
            if (statusMessage) {
                if (progress < 30) {
                    statusMessage.textContent = 'Subiendo archivo...';
                } else if (progress < 95) {
                    statusMessage.textContent = `Procesando datos del archivo... (${Math.round(progress)}% completado)`;
                } else {
                    statusMessage.textContent = 'Finalizando procesamiento...';
                }
            }
        });

        connection.start().catch(function (err) {
            console.error('Error al conectar con SignalR:', err.toString());
        });
    }

})();
