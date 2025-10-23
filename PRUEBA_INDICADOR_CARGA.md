# Prueba del Indicador de Carga

## Cambios Realizados

### 1. Archivos Modificados

- ✅ [Views/Shared/_Layout.cshtml](FacturacionSimple/Views/Shared/_Layout.cshtml) - Descomentados los scripts
- ✅ [Views/Home/Index.cshtml](FacturacionSimple/Views/Home/Index.cshtml) - Corregida estructura del formulario
- ✅ [wwwroot/js/site.js](FacturacionSimple/wwwroot/js/site.js) - Agregado logging para depuración
- ✅ [wwwroot/css/site.css](FacturacionSimple/wwwroot/css/site.css) - Estilos del indicador de carga

### 2. Problemas Corregidos

1. **Formularios anidados:** Había dos formularios anidados causando conflicto
2. **Scripts comentados:** Los scripts de jQuery, Bootstrap y site.js estaban comentados
3. **Selector incorrecto:** El JavaScript buscaba cualquier form, ahora busca específicamente `#uploadForm`

## Cómo Probar

### Paso 1: Ejecutar la Aplicación

```bash
cd /Users/urielfraidenraij_1/Projects/FacturacionAdmin/FacturacionSimple
dotnet run
```

### Paso 2: Abrir el Navegador

1. Navega a `https://localhost:5001` (o el puerto que use tu aplicación)
2. Abre la **Consola de Desarrollador** (F12 en Chrome/Firefox)

### Paso 3: Verificar los Logs en la Consola

Deberías ver estos mensajes en la consola:

```
✓ Site.js cargado correctamente
✓ Scripts de Index.cshtml cargados
✓ jQuery versión: 3.7.1
✓ Formulario de upload encontrado
○ Overlay de carga se creará al enviar el formulario
```

### Paso 4: Probar el Indicador de Carga

1. Selecciona un archivo Excel (.xlsx)
2. Haz clic en el botón **"Subir"**
3. **Deberías ver:**
   - Un overlay oscuro cubriendo toda la pantalla
   - Un spinner/loading animado
   - Una barra de progreso
   - El nombre y tamaño del archivo
   - Mensajes de estado que cambian:
     - "Subiendo archivo..."
     - "Procesando datos del archivo..."
     - "Finalizando..."

## Si el Indicador NO Aparece

### Verificación 1: Consola del Navegador

Revisa la consola del navegador (F12) y busca:

**❌ Si ves errores como:**
```
✗ Formulario de upload NO encontrado
```

**Solución:** El formulario no se está encontrando. Verifica que estés en la página principal.

**❌ Si ves errores como:**
```
Uncaught ReferenceError: $ is not defined
```

**Solución:** jQuery no se cargó. Verifica que los scripts estén descomentados en _Layout.cshtml.

**❌ Si ves:**
```
Failed to load resource: the server responded with a status of 404 (Not Found) site.js
```

**Solución:** El archivo site.js no se encuentra. Verifica que existe en `wwwroot/js/site.js`.

### Verificación 2: Network Tab

1. Abre las DevTools (F12)
2. Ve a la pestaña "Network"
3. Recarga la página
4. Verifica que estos archivos se carguen:
   - `site.js` (200 OK)
   - `site.css` (200 OK)
   - `jquery.min.js` (200 OK)
   - `bootstrap.bundle.min.js` (200 OK)

### Verificación 3: Elementos HTML

1. Inspecciona el formulario en la página
2. Verifica que tenga el atributo `id="uploadForm"`
3. Verifica que el input de archivo tenga `type="file"` y `name="file"`

## Debugging Manual

Si todavía no funciona, ejecuta esto en la consola del navegador:

```javascript
// Verificar que jQuery está cargado
console.log('jQuery:', typeof $);

// Verificar que el formulario existe
console.log('Formulario:', document.getElementById('uploadForm'));

// Verificar que el input existe
console.log('Input:', document.querySelector('#uploadForm input[type="file"]'));

// Probar crear el overlay manualmente
const overlay = document.createElement('div');
overlay.id = 'loading-overlay';
overlay.className = 'loading-overlay active';
overlay.innerHTML = `
    <div class="loading-content">
        <div class="spinner"></div>
        <h4>PRUEBA - Procesando archivo...</h4>
        <div class="progress-bar-container">
            <div class="progress-bar" style="width: 50%">
                <span>50%</span>
            </div>
        </div>
        <p class="loading-text">Esto es una prueba</p>
    </div>
`;
document.body.appendChild(overlay);
```

Si ejecutas el código anterior y VES el overlay, significa que:
- ✅ El CSS está funcionando
- ✅ El problema está en el JavaScript que detecta el submit del formulario

## Prueba Alternativa - Mostrar Overlay Siempre

Si quieres forzar que el overlay aparezca al cargar la página (solo para prueba), agrega esto temporalmente al final de `site.js`:

```javascript
// SOLO PARA PRUEBA - BORRAR DESPUÉS
setTimeout(function() {
    const testFile = { name: 'test.xlsx', size: 1024000 };
    showLoadingOverlay(testFile);
}, 2000);
```

## Información de Contacto

Si después de estas pruebas el indicador aún no aparece, necesitaré:

1. Captura de pantalla de la consola del navegador
2. Captura de pantalla de la pestaña Network
3. El resultado de ejecutar el código de debugging manual

---

**Última actualización:** Octubre 2025
