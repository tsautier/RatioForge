# Release policy

1. Prevoir systematiquement un versionnement SemVer pour toute livraison de modifications.
2. Conserver la meme version dans `version.txt`, les projets, la documentation et les notes de version.
3. Mettre a jour `CHANGELOG.md` et les notes de release avant chaque livraison.
4. Executer l'ensemble des tests, audits, builds, smoke checks et controles d'artefacts avant de publier.
5. Creer et pousser le tag Git correspondant au format `vX.Y.Z`, puis creer la GitHub Release associee.
6. Publier les executables, archives et fichiers SHA256 prevus pour Windows, Linux et macOS.
7. Verifier apres publication que la GitHub Release et tous ses artefacts sont accessibles, non vides et conformes aux checksums.
8. Ne jamais incrementer, remplacer ou republier une version lorsque l'utilisateur a explicitement demande de la conserver. Dans ce cas, attendre son autorisation avant toute nouvelle version ou release.
