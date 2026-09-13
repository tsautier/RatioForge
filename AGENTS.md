# Release policy

1. Prevoir systematiquement un versionnement SemVer pour toute livraison de modifications.
2. Conserver la meme version dans `version.txt`, les projets, la documentation et les notes de version.
3. Mettre a jour `CHANGELOG.md` et les notes de release avant chaque livraison.
4. Executer l'ensemble des tests, audits, builds, smoke checks et controles d'artefacts avant de publier.
5. Creer et pousser le tag Git correspondant au format `vX.Y.Z`, puis creer la GitHub Release associee.
6. Publier les executables, archives et fichiers SHA256 prevus pour Windows, Linux et macOS.
7. Verifier apres publication que la GitHub Release et tous ses artefacts sont accessibles, non vides et conformes aux checksums.
8. Interpreter une demande de conserver une version comme l'obligation de publier cette version si son tag ou sa GitHub Release n'existe pas encore, puis de ne pas passer a une version superieure sans autorisation.
9. Ne jamais confondre un gel de version avec une interdiction de publier la version gelee. Avant de terminer une livraison, verifier explicitement le tag et la GitHub Release avec `git ls-remote --tags` et `gh release view`.
10. Ne jamais incrementer, remplacer ou republier une version deja publiee lorsque l'utilisateur a explicitement demande de la conserver. Attendre son autorisation avant toute version superieure.
