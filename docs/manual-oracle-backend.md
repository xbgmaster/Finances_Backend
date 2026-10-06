# Manual: API de Tishe en Oracle Cloud (Always Free)

La API queda encendida todo el tiempo en una máquina Ubuntu de Oracle. Escucha por HTTP en el puerto 8080. El HTTPS lo pone Cloudflare (ver `manual-cloudflare.md`).

No pulses **Upgrade** en Oracle. La cuenta debe quedarse en Always Free.

Datos de esta instalación:

| Dato | Valor |
| --- | --- |
| Instancia | `tishe-api` |
| Región | Canada Southeast (Montreal) |
| Imagen | Ubuntu 22.04 |
| Usuario SSH | `ubuntu` |
| IP pública | `147.15.134.85` |
| API directa | `http://147.15.134.85:8080` |
| Repositorio | `https://github.com/xbgmaster/Finances_Backend.git` |

## 1. Red y puertos en la consola de Oracle

1. Entra a [Oracle Cloud](https://cloud.oracle.com/).
2. Menú → **Networking** → **Virtual cloud networks**.
3. Abre la VCN de la instancia (`vcn-20261005-1518`).
4. Abre la **Security List** por defecto → **Security rules** → **Add Ingress Rules**.

Reglas de entrada, origen `0.0.0.0/0`, protocolo TCP:

| Puerto destino | Para qué |
| --- | --- |
| 22 | SSH |
| 80 | HTTP (certificados, más adelante) |
| 443 | HTTPS (más adelante) |
| 8080 | La API |

El puerto de origen se deja en **All**.

## 2. Entrar por SSH desde Windows

La clave privada está en `C:\Users\aleja\.ssh\tishe-oracle.key`. OneDrive abre los permisos de más y OpenSSH rechaza la clave, por eso no se usa la copia del escritorio.

En PowerShell:

```powershell
ssh -i "C:\Users\aleja\.ssh\tishe-oracle.key" ubuntu@147.15.134.85
```

La primera vez pregunta si confías en el servidor. Escribe `yes`. El prompt correcto es `ubuntu@vnicname:~$`. Todo lo que sigue va en esa ventana, no en PowerShell.

Si Windows dice `UNPROTECTED PRIVATE KEY FILE`, copia la clave fuera de OneDrive y cierra los permisos:

```powershell
mkdir C:\Users\aleja\.ssh -Force
Copy-Item ".\private-ssh-key-2026-10-05.key" "C:\Users\aleja\.ssh\tishe-oracle.key"
icacls "C:\Users\aleja\.ssh\tishe-oracle.key" /inheritance:r
icacls "C:\Users\aleja\.ssh\tishe-oracle.key" /grant:r "$($env:USERNAME):(R)"
```

## 3. Abrir los puertos dentro de Ubuntu

La lista de Oracle no basta. Ubuntu también filtra con iptables.

```bash
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 80 -j ACCEPT
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 443 -j ACCEPT
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 8080 -j ACCEPT
sudo apt-get update
sudo apt-get install -y iptables-persistent
sudo netfilter-persistent save
```

Si pregunta si quieres guardar las reglas, responde **Yes**.

## 4. Docker y el código

```bash
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker ubuntu
git clone https://github.com/xbgmaster/Finances_Backend.git
cd Finances_Backend
```

`docker build` se ejecuta aquí, en el servidor. Si lo corres en PowerShell de tu PC, falla porque Docker Desktop no está instalado.

## 5. Archivo `api.env`

En el servidor, dentro de `~/Finances_Backend`:

```bash
nano api.env
```

Cada valor en una sola línea, sin comillas y con **dos** guiones bajos. Un guion bajo no lo lee .NET. La cadena de Neon tiene que estar completa. No pegues este archivo en el chat.

```text
ASPNETCORE_ENVIRONMENT=Production
KEYVAULT_URI=https://kvleaping.vault.azure.net/
AZURE_CLIENT_ID=el-client-id-de-azure
AZURE_TENANT_ID=el-tenant-id-de-azure
AZURE_CLIENT_SECRET=el-secreto-de-azure
ConnectionStrings__DefaultConnection=Host=...;Database=neondb;Username=neondb_owner;Password=...;SSL Mode=Require;Trust Server Certificate=true
Email__Provider=brevo
Email__ApiKey=la-clave-de-brevo
Email__From=xbgalejandro@gmail.com
Email__FromName=Finances
Google__ClientId=487523170626-r9hgc3ocogoiuthbv9tbdinnhln7n23p.apps.googleusercontent.com
Google__AndroidClientId=487523170626-o51lhv7tok8vfsa446gf4mvccj4s6v85.apps.googleusercontent.com
App__FrontendUrl=https://tishe.tishe.workers.dev
```

Guarda con **Ctrl+O**, Enter, y sal con **Ctrl+X**.

`Jwt__Key` sale de Azure Key Vault, igual que en Render. Si faltan las tres variables `AZURE_`, el contenedor se reinicia y el log habla de Azure CLI.

## 6. Compilar y arrancar

La máquina gratis tiene poca RAM. El archivo de intercambio evita que la compilación muera.

```bash
sudo fallocate -l 2G /swapfile
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile
sudo docker build -t tishe-api .
sudo docker rm -f tishe-api
sudo docker run -d --name tishe-api --restart unless-stopped --env-file api.env -p 8080:8080 tishe-api
```

La primera compilación tarda varios minutos. Si cambias `api.env`, hay que borrar el contenedor y volver a ejecutar el `docker run`. Editar el archivo no actualiza el contenedor que ya está corriendo.

## 7. Comprobar

En el servidor:

```bash
curl -sS http://127.0.0.1:8080/api/auth/google-client
```

Respuesta esperada:

```json
{"clientId":"487523170626-r9hgc3ocogoiuthbv9tbdinnhln7n23p.apps.googleusercontent.com"}
```

Desde el navegador de tu PC:

`http://147.15.134.85:8080/api/auth/google-client`

Swagger:

`http://147.15.134.85:8080/swagger/index.html`

Si `curl` dice `Connection reset by peer`:

```bash
sudo docker ps -a
sudo docker logs tishe-api --tail 100
```

`Up` unos pocos segundos significa que el proceso se está cayendo y Docker lo vuelve a levantar. Revisa que `api.env` tenga las variables de Azure y que la cadena de Neon no esté cortada.

Para ver solo los nombres de variables que recibió el contenedor, sin valores:

```bash
sudo docker inspect tishe-api --format '{{range .Config.Env}}{{println .}}{{end}}' | cut -d= -f1
```

## 8. Actualizar el código más adelante

```bash
cd ~/Finances_Backend
git pull
sudo docker build -t tishe-api .
sudo docker rm -f tishe-api
sudo docker run -d --name tishe-api --restart unless-stopped --env-file api.env -p 8080:8080 tishe-api
```

El CORS de la página ya está en `src/Finances.Api/Program.cs` como `https://tishe.tishe.workers.dev`. Si cambias la dirección de la página, añádela ahí y vuelve a construir la imagen.
