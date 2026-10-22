# Base de données — `LogisticsDb`

## À lire d'abord : ce dossier contient déjà un outillage

Un travail antérieur (voir [`Reports/LocalDB-Report-2026-10-12.md`](Reports/LocalDB-Report-2026-10-12.md)) a mis en
place `Scripts/Inspect-Database.sql`, `Scripts/Backup-Database.sql`, `Scripts/Restore-Database.sql` et l'outil
`Tools/DatabaseVerifier`. Ce travail visait à **restaurer la vraie base legacy**, qui n'est pas disponible :
aucune sauvegarde SQL Server du projet n'existe sur cette machine.

Ce qui a été ajouté ici répond à une demande différente : **créer une base de test utilisable**, avec des données
inventées, pour pouvoir travailler sans attendre la vraie source.

> **Ces données ne sont pas les données de production.** Elles sont fabriquées. L'API fonctionnera, les écrans
> afficheront quelque chose, mais aucun de ces clients, dossiers ou montants n'a d'existence réelle. Le rapport
> du 12 octobre recommandait explicitement de ne pas créer de base vide pour contourner l'erreur 4060 ; c'est ici
> un choix assumé et différent, pour disposer d'un environnement de test.

## Ce que je n'ai pas pu faire, et pourquoi

**Le `.bak` n'a pas été produit ici.** Un `.bak` est écrit *par le moteur SQL Server* : sans connexion, il est
impossible à fabriquer hors ligne. Or je n'ai aucune connexion à l'instance depuis mon environnement
d'exécution — TCP est désactivé (`SuperSocketNetLib\Tcp\Enabled = 0`), l'instance n'est donc joignable que par
mémoire partagée ou canaux nommés, transports que le bac à sable bloque. L'erreur observée était
*« erreur 26 — Erreur lors de la localisation du serveur/de l'instance spécifiés »*.

**Correction d'une conclusion que j'avais tirée trop vite.** En voyant l'échec, j'ai d'abord conclu que la chaîne
`Encrypt=True;TrustServerCertificate=False` était fautive, et je l'ai modifiée. **C'était une erreur.** Le rapport
du 12 octobre établit que cette chaîne **fonctionne** sur cette machine, hors bac à sable, et que ce sont les
restrictions du bac à sable qui avaient déjà empêché les premiers essais LocalDB. J'ai donc rétabli les deux
chaînes à l'identique. Le pilote ODBC y ajoutait sa propre panne (*« Chiffrement non pris en charge sur le
client »*) : un problème de transport qui se présente comme un problème TLS. **Ne relâchez pas `Encrypt` sur la
foi d'un échec observé dans un shell restreint.**

La leçon vaut d'être notée : j'ai failli livrer une régression dans une configuration vérifiée, en corrigeant un
problème qui n'existait que chez moi.

## Marche à suivre, sur votre machine

```powershell
cd "AI LOGTISTICS BACKEND"

# 1. créer la base de test
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -i Database\Scripts\Create-TestDatabase.sql

# 2. créer les 96 tables
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -d LogisticsDb -i Database\Scripts\Create-Schema.sql

# 3. insérer le jeu de test
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -d LogisticsDb -i Database\Scripts\Seed-TestData.sql

# 4. produire le .bak — script existant, plus rigoureux
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -i Database\Scripts\Backup-Database.sql
```

`Backup-Database.sql` vérifie que l'instance est bien LocalDB, refuse d'écraser un fichier existant, sauvegarde en
`COPY_ONLY, CHECKSUM`, puis enchaîne `VERIFYONLY`, `HEADERONLY` et `FILELISTONLY`. Il écrit dans
`Database\Backups\`.

> **À ajuster avant de lancer l'étape 4 :** le script code en dur le nom
> `Database\Backups\LogisticsDb_2026-10-12.bak`. Changez la date dans `@BackupFile` pour un nom de votre choix,
> sinon le script refusera d'écraser — ce qui est son comportement voulu.

Les étapes 1 à 3 sont **idempotentes** : les relancer ne détruit rien et ne duplique rien. Seule l'étape 4 refuse
de s'exécuter deux fois, volontairement.

Le jeu de données inséré : 1 société, 2 agences, 3 devises, 3 pays, 2 clients, 2 comptes utilisateurs, plus les
référentiels (types de dossier, ports, incoterms, COD, classes client) — **24 insertions, 144 colonnes**, chacune
vérifiée contre le schéma généré.

## Identifiants créés

| Identifiant | Mot de passe | Rôle |
|---|---|---|
| `admin` | `Admin123!` | administrateur (`admin = 1`), accès au paramétrage |
| `dupont` | `User123!` | utilisateur ordinaire |

Ce sont les valeurs de `t_personnel.identifiant_app`, pour la société `S01`.

Les mots de passe du script ne sont **pas** des valeurs collées en dur : ils sont produits par
`MotDePasseLegacy.Coder`, l'algorithme que l'API vérifie à la connexion. Un test le prouve de bout en bout —
`Admin123!` donne `28258971` — et il échouerait si l'encodeur et le vérificateur divergeaient, au lieu de vous
laisser chercher un faux « mot de passe incorrect ».

## Chaîne de connexion

Elle est **inchangée** et vérifiée :

```text
Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=LogisticsDb;Integrated Security=True;Persist Security Info=False;Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=False;Application Name=Logistics.Api;Connect Timeout=15
```

Si vous préférez l'instance **SQL Express** (le service `MSSQL$SQLEXPRESS` tourne aussi sur cette machine),
remplacez seulement `Data Source` :

```text
Data Source=.\SQLEXPRESS;Initial Catalog=LogisticsDb;...
```

Dans les deux cas, `Encrypt=True` reste correct en local. En production, utilisez un certificat réellement
approuvé plutôt que de désactiver le chiffrement.

Après l'étape 3, l'erreur **4060** qui apparaissait sur `/api/v1/clients` disparaît : la base existe.

## Régénérer le schéma et le jeu de données

`Create-Schema.sql` et `Seed-TestData.sql` sont **générés**, pas écrits à la main :

```powershell
cd "AI LOGTISTICS BACKEND"
dotnet build Database\Tools\DbScript\Logistics.DbScript.csproj -m:1

dotnet Database\Tools\DbScript\bin\Debug\net10.0\Logistics.DbScript.dll `
    schema Database\Scripts\Create-Schema.sql LogisticsDb

dotnet Database\Tools\DbScript\bin\Debug\net10.0\Logistics.DbScript.dll `
    seed   Database\Scripts\Seed-TestData.sql
```

Le schéma est dérivé du modèle EF, il ne peut donc pas diverger de ce que l'application attend. C'est important
ici : le projet legacy ne contient **aucun DDL** — pas un seul `.sql`, `.mdf` ou `.bak` — le schéma a été
reconstitué à partir des chaînes SQL littérales du code VB. Ce script est la seule définition faisant autorité
pour ces tables, et il correspond à l'inventaire du rapport : **96 tables, 83 index, 21 relations FK**.

> Lancez le `.dll` directement plutôt que `dotnet run -- ...` : `dotnet run` transmet à l'application les options
> qu'il ne reconnaît pas, et `-m:1` s'est retrouvé interprété comme nom de fichier de sortie.

## Avant de considérer le schéma comme définitif

Le DDL est **déduit** du code legacy, pas extrait d'une base existante. Si vous remettez la main sur une base
TransmarNetref réelle, comparez avant de vous fier à ces noms de colonnes :

```sql
SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN (SELECT name FROM sys.tables)
ORDER BY TABLE_NAME, ORDINAL_POSITION;
```

`Scripts/Inspect-Database.sql` fait déjà cet inventaire, en plus des contraintes, index, triggers et comptes de
lignes. Les écarts les plus probables sont les longueurs de colonnes et les colonnes `NOT NULL` ajoutées après
coup.

## Précision des décimales — un défaut corrigé

En lisant les journaux de l'API, j'ai trouvé **76 propriétés décimales sans précision explicite**. EF Core les
signalait lui-même à chaque démarrage à froid :

> *No store type was specified for the decimal property ... This will cause values to be **silently truncated** if
> they do not fit in the default precision and scale.*

Elles tombaient donc à `decimal(18,2)`. Or la liste n'était pas anodine : `Dossier.Volume`,
`Dossier.VolumeTaxable`, `ColisageDossier.Volume`, `CoutKilometrageGasoil.RatioConsommation`,
`Dossier.TauxChangeValMarch`. Et **le domaine calcule les volumes à trois décimales** — `MesuresTaxables` et le
lecteur EDI travaillent tous deux à 3 dp. Un volume de 12,345 était donc stocké 12,34, **sans aucune erreur
levée**. Une mesure taxable tronquée, c'est une facture fausse, pas un défaut cosmétique.

Corrigé par une convention sur le modèle : la précision par défaut des `decimal` passe à **(18,3)**. Une
configuration explicite reste prioritaire.

| Avant | Après |
|---|---|
| 168 colonnes en `decimal(18,2)`, dont 76 par défaut subi | 204 en `(18,3)`, 8 en `(18,6)`, 7 en `(9,4)`, 4 en `(18,4)`, 3 en `(18,2)` |
| avertissements EF `Model.Validation` à chaque démarrage | **0** |
| colonnes décimales sans précision : 76 | **0** sur 226 |

**Le compromis, à connaître.** Seules **3** colonnes restent explicitement en `(18,2)`. Les montants qui ne sont
pas configurés explicitement sont donc désormais stockés à 3 décimales, alors que le legacy utilise
vraisemblablement 2 dp pour la monnaie. C'est un écart de schéma, pas une corruption : une décimale
supplémentaire n'altère aucune valeur, tandis qu'une décimale manquante en détruit. À réconcilier avec la vraie
base via `Inspect-Database.sql` — comme le reste du schéma, qui est déduit et non extrait.
