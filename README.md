# Repo de départ pour le projet **Smartlease** avec BalenaCloud

Smartlease est un projet IoT embarqué destiné à la surveillance d'activités via des capteurs de mouvement placés dans des matelas. Il capture, stocke et transmet les données à un cloud Azure IoT Hub depuis une flotte de Raspberry Pi.

# Structure du projet
smartlease/
├── docker-compose.yml
├── README.md
├── antenna-capture/         # Service 1 : C pour capture antenne
│   ├── Dockerfile
│   ├── main.c
│   ├── db.c
│   ├── db.h
│   └── Makefile
├── azure-uploader/          # Service 2 : C# pour envoi Azure IoT
│   ├── Dockerfile
│   ├── Program.cs
│   ├── AzureUploader.csproj
│   └── appsettings.json
└── shared/
    └── database/            # Volume partagé pour SQLite

# Format des données capturées
Les capteurs émettent toutes les **5 minutes** et réitèrent les données toutes les **2.5 minutes**.

Exemple de trame reçue : `030BA0020C9E2A0000006603`

- `030B` → **Entête**
- `A0020C9E` → **ID unique du capteur**
- `2A` → **Compteur**: `2` est fixe, la seconde valeur varie de `8` à `F`
- `00000066` → **Valeur de mouvement** sur 1 octet, shifté pour une petite historique
- `03` → **Orientation** : déduite via bitmasking

Calcul de l'orientation (extrait C#) :
```csharp
orientation = (Convert.ToInt32(extractedOrientation, 16) & ORIENTATION_THRESHOLD) > 0 ? 1 : 0;
```

# Fonctionnement global
- `antenna-capture/` : lecture des trames depuis le module NRF905, décodage et stockage local (SQLite).
  (docker run --rm -it --privileged --device=/dev/ttyACM0 antenna-capture ./antenna_capture /dev/ttyACM0)
- `azure-uploader/` : lecture de la base SQLite, transmission des données vers Azure IoT Hub.
