# The Game Of Life

Une implémentation WPF de l'automate cellulaire de Conway.

## Utilisation

- Cliquez sur les cases pour créer ou supprimer des cellules vivantes.
- **Démarrer / Pause** lance ou suspend l'évolution automatique.
- **Avancer** calcule une génération.
- **Aléatoire** crée une configuration de départ aléatoire.
- **Effacer** vide la grille et remet le compteur à zéro.
- Réglez la vitesse avec le curseur (de 1 à 20 générations par seconde).

La grille mesure 60 × 35 cellules. Les cellules situées au-delà de ses bords sont considérées comme mortes. Une cellule vivante survit avec deux ou trois voisines vivantes ; une cellule morte naît avec exactement trois voisines vivantes.
