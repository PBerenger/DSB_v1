# Double Sceens Black

Application Windows en WPF pour assombrir un écran ou tous les écrans avec un voile noir réglable et un raccourci clavier global.

## Utilisation

- La fenêtre principale affiche les écrans connectés. « Actualiser » recharge la liste.
- Activez « Tous les écrans » pour régler tous les écrans avec un même potard. Désactivez cette option pour régler chaque écran indépendamment.
- À la première ouverture, « Tous les écrans » est activé par défaut.
- La liste des écrans devient défilante lorsqu'elle dépasse l'espace visible.
- La flèche à droite de « Écrans » ouvre « Déplacements ». La flèche à gauche de cette zone revient aux écrans. Choisissez une fenêtre ouverte et un écran de destination ; le mode « Simple » centre la fenêtre en conservant sa taille, tandis que le mode « Agrandi » l'adapte à l'écran.
- Le bouton Options reste disponible en haut à droite des deux panneaux ; les flèches de navigation sont placées plus bas dans la fenêtre.
- La liste des déplacements propose les fenêtres visibles de la barre des tâches, en excluant notamment les fenêtres outils et celles masquées par Windows ; les fenêtres de Double Sceens Black sont également exclues. Actualisez la liste pour la mettre à jour.
- Le bouton « Identifier » affiche brièvement le numéro attribué à l'écran. Une nouvelle identification remplace la précédente.
- Les intensités de chaque écran et le mode commun/individuel sont enregistrés avec les autres préférences.
- Dans « Options », utilisez « Modifier » pour définir le raccourci global, ou « Par défaut » pour rétablir Ctrl + Alt + B.
- Les intensités par défaut des écrans et le réglage commun commencent à 0 % ; les valeurs déjà enregistrées sont conservées.
- Utilisez le bouton ou le raccourci pour assombrir puis rétablir les écrans.
- Une confirmation s'affiche avant d'assombrir tous les écrans à 100 %, avec le raccourci global actuel pour pouvoir rétablir l'affichage.
- Le bouton « Options » ouvre une fenêtre modale qui permet de choisir le thème bleu ou noir, de configurer le raccourci global, d'activer le démarrage avec Windows et de masquer l'application dans la zone de notification au lieu de la fermer avec la croix. Si ces deux dernières options sont activées, l'application démarre directement réduite dans la zone de notification. Double-cliquez sur son logo pour la rouvrir, ou utilisez le menu contextuel pour l'ouvrir ou quitter l'application.

Le raccourci est actif tant que l'application est ouverte. Les préférences d'affichage sont enregistrées dans `%LOCALAPPDATA%\DoubleSceensBlack\settings.json`. L'option de démarrage utilise l'entrée de démarrage de l'utilisateur Windows courant.

L'application place une fenêtre noire transparente au-dessus des écrans sélectionnés. Elle ne modifie pas le rétroéclairage et ne coupe pas leur alimentation.

## Compiler

Installer le SDK .NET 8, puis lancer depuis ce dossier :

```powershell
dotnet build
dotnet run
```

## Créer l’installeur Windows

Installer le SDK .NET 8 et Inno Setup 6, puis lancer depuis la racine du projet :

```powershell
dotnet publish .\DoubleSceensBlack.csproj --configuration Release --runtime win-x64 --self-contained true --output .\Executable\publish
ISCC.exe .\Executable\BSD_installer.iss
```

Le fichier `Executable\BSD_installer.exe` installe l’application pour tous les utilisateurs et demande les droits administrateur. L’icône de configuration est générée depuis `Ressources\Logos\Logo_DSB.png`.
