# Manual: página y HTTPS de la API en Cloudflare

Hay dos workers. No se mezclan.

| Worker | Dirección | Qué es |
| --- | --- | --- |
| `tishe` | `https://tishe.tishe.workers.dev` | La página (archivos de `dist`) |
| `tishe-api` | `https://tishe-api.tishe.workers.dev` | Puente https hacia la API de Oracle |

La página es https. El navegador no deja que llame a `http://147.15.134.85:8080`. El worker `tishe-api` recibe la llamada en https y, por detrás, la pasa a Oracle. Un Worker no puede llamar a una IP directa (error 1003), así que el destino usa el nombre `147.15.134.85.nip.io`, que apunta a la misma IP.

Consola: [Cloudflare Workers](https://dash.cloudflare.com/) → **Workers & Pages**.

## 1. Worker de la API

1. **Create application** → **Start with Hello World!**.
2. Nombre: `tishe-api`. La vista previa del código no se puede editar.
3. Deja **Protect with Cloudflare Access** apagado y pulsa **Deploy**.
4. Abre el worker → **Edit code**. Borra el Hello World y pega esto:

```javascript
export default {
  async fetch(request) {
    const url = new URL(request.url);
    const target = new URL(url.pathname + url.search, "http://147.15.134.85.nip.io:8080");
    return fetch(target, {
      method: request.method,
      headers: request.headers,
      body: request.method === "GET" || request.method === "HEAD" ? undefined : request.body,
      redirect: "manual",
    });
  },
};
```

5. Pulsa **Deploy**.

Pruebas en el navegador:

- `https://tishe-api.tishe.workers.dev/api/auth/google-client`
- `https://tishe-api.tishe.workers.dev/swagger/index.html`

Tiene que salir el mismo JSON y el mismo Swagger que en `http://147.15.134.85:8080`.

## 2. Apuntar la página a esa API

Esto se hace en el proyecto del frontend, en tu PC. El backend de Oracle no cambia de URL.

En `.env.production`:

```text
VITE_API_URL=https://tishe-api.tishe.workers.dev/api
```

`npm run build` solo genera la carpeta `dist`. No publica. En PowerShell, dentro de la carpeta del frontend:

```powershell
npm run build
```

Luego, en Cloudflare, abre el worker **`tishe`** (la página) y vuelve a subir el contenido de `dist`, de la misma forma que la primera publicación. El worker `tishe-api` no se vuelve a tocar.

Entra a `https://tishe.tishe.workers.dev` e inicia sesión.

## 3. Si el login dice CORS

El origen permitido tiene que existir en el código que está corriendo en Oracle:

`https://tishe.tishe.workers.dev`

Está en `src/Finances.Api/Program.cs`. Si añades otra dirección de página, agrégala ahí, haz `git push`, y en el servidor:

```bash
cd ~/Finances_Backend
git pull
sudo docker build -t tishe-api .
sudo docker rm -f tishe-api
sudo docker run -d --name tishe-api --restart unless-stopped --env-file api.env -p 8080:8080 tishe-api
```

También hay que poner esa dirección en el cliente web de Google, en **Authorized JavaScript origins**:

`https://tishe.tishe.workers.dev`

El client id web es `487523170626-r9hgc3ocogoiuthbv9tbdinnhln7n23p.apps.googleusercontent.com`.
