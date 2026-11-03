# The Game Of Life

Une implémentation WPF de l'automate cellulaire de Conway.

## Utilisation

- Cliquez sur les cases pour créer ou supprimer des cellules vivantes.
- **Démarrer / Pause** lance ou suspend l'évolution automatique.
- **Avancer** calcule une génération.
- **Aléatoire** crée une configuration de départ aléatoire.
- **Effacer** vide la grille et remet le compteur à zéro.
- **Enregistrer** sauvegarde la grille, la génération et le nombre de cellules vivantes dans un fichier JSON.
- **Charger** restaure une partie enregistrée et met la simulation en pause.
- Réglez la vitesse avec le curseur (de 1 à 20 générations par seconde).

La grille mesure 60 × 35 cellules. Les cellules situées au-delà de ses bords sont considérées comme mortes. Une cellule vivante survit avec deux ou trois voisines vivantes ; une cellule morte naît avec exactement trois voisines vivantes.

La simulation se met en pause si la grille revient à un état déjà rencontré au cours des 10 000 dernières générations, ce qui détecte également les oscillateurs.

La taille et la position de la fenêtre sont enregistrées à sa fermeture dans `window-settings.json`, à côté de l'application, puis restaurées au prochain démarrage. L'application doit donc avoir le droit d'écriture dans son répertoire.
