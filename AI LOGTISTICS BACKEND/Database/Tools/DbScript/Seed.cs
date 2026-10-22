using Logistics.Domain.Entities.Resources;

namespace Logistics.DbScript;

/// <summary>
/// Emits a small, coherent test dataset.
/// </summary>
/// <remarks>
/// <para>
/// The passwords are encoded with <see cref="MotDePasseLegacy"/> — the same routine the API verifies against —
/// rather than pasted as literals. A hand-written hash would silently stop working the day the encoding is
/// corrected, and the failure would look like a wrong password rather than a stale seed.
/// </para>
/// <para>
/// Every insert is guarded, so the script can be run twice without duplicating rows or failing on the primary
/// key. A seed that only works on an empty database is a seed that has to be explained.
/// </para>
/// </remarks>
internal static class Seed
{
    public static string Generer(string clairAdmin, string clairUtilisateur)
    {
        var admin = MotDePasseLegacy.Coder(clairAdmin);
        var utilisateur = MotDePasseLegacy.Coder(clairUtilisateur);

        return $"""
-- ============================================================================================
--  LogisticsDb — jeu de donnees de test
-- ============================================================================================
--  Coherent et idempotent : chaque insertion est protegee par IF NOT EXISTS, donc le script peut
--  etre relance sans creer de doublons ni echouer sur une cle primaire.
--
--  Les deux mots de passe sont encodes par MotDePasseLegacy, l'algorithme que l'API verifie —
--  pas des valeurs collees en dur, qui cesseraient de fonctionner des que l'encodage serait
--  corrige, en produisant une erreur ressemblant a un mauvais mot de passe.
--
--    admin / {clairAdmin}   -> administrateur (role de parametrage)
--    dupont / {clairUtilisateur}   -> utilisateur ordinaire
--
--  Les identifiants sont ceux du champ identifiant_app de t_personnel.
-- ============================================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- ---------------------------------------------------------------- societe et agences
IF NOT EXISTS (SELECT 1 FROM [t_societe] WHERE [key_soc] = 'S01')
    INSERT INTO [t_societe] ([key_soc],[intitule_soc],[soc_abrege],[devise_soc],[pays_soc],[ville_soc],[langue])
    VALUES ('S01', N'TRANS MAROC LOGISTIQUE', N'TML', 'MAD', 'MA', N'Casablanca', 'FR');

IF NOT EXISTS (SELECT 1 FROM [t_agence] WHERE [key_agence] = 'AG01')
    INSERT INTO [t_agence] ([key_agence],[soc_agence],[code_agence],[intitule_agence],[ville_agence],[pays_agence],[scan_MEAD],[supp])
    VALUES ('AG01', 'S01', 'CASA', N'Agence Casablanca', N'Casablanca', 'MA', 0, 0);

IF NOT EXISTS (SELECT 1 FROM [t_agence] WHERE [key_agence] = 'AG02')
    INSERT INTO [t_agence] ([key_agence],[soc_agence],[code_agence],[intitule_agence],[ville_agence],[pays_agence],[scan_MEAD],[supp])
    VALUES ('AG02', 'S01', 'TNG', N'Agence Tanger', N'Tanger', 'MA', 0, 0);

-- ---------------------------------------------------------------- devises
IF NOT EXISTS (SELECT 1 FROM [t_devise] WHERE [code_devise] = 'MAD')
    INSERT INTO [t_devise] ([code_devise],[libelle_devise],[nb_decimal],[devise_edi]) VALUES ('MAD', N'Dirham marocain', 2, 'MAD');
IF NOT EXISTS (SELECT 1 FROM [t_devise] WHERE [code_devise] = 'EUR')
    INSERT INTO [t_devise] ([code_devise],[libelle_devise],[nb_decimal],[devise_edi]) VALUES ('EUR', N'Euro', 2, 'EUR');
IF NOT EXISTS (SELECT 1 FROM [t_devise] WHERE [code_devise] = 'USD')
    INSERT INTO [t_devise] ([code_devise],[libelle_devise],[nb_decimal],[devise_edi]) VALUES ('USD', N'Dollar americain', 2, 'USD');

-- ---------------------------------------------------------------- pays
IF NOT EXISTS (SELECT 1 FROM [T_PAYS] WHERE [CODE_PAYS] = 'MA')
    INSERT INTO [T_PAYS] ([CODE_PAYS],[INTITULE_PAYS],[ALPHA2]) VALUES ('MA', N'MAROC', 'MA');
IF NOT EXISTS (SELECT 1 FROM [T_PAYS] WHERE [CODE_PAYS] = 'FR')
    INSERT INTO [T_PAYS] ([CODE_PAYS],[INTITULE_PAYS],[ALPHA2]) VALUES ('FR', N'FRANCE', 'FR');
IF NOT EXISTS (SELECT 1 FROM [T_PAYS] WHERE [CODE_PAYS] = 'ES')
    INSERT INTO [T_PAYS] ([CODE_PAYS],[INTITULE_PAYS],[ALPHA2]) VALUES ('ES', N'ESPAGNE', 'ES');

-- ---------------------------------------------------------------- referentiels
IF NOT EXISTS (SELECT 1 FROM [t_type_dossier] WHERE [type_dossier] = 'I')
    INSERT INTO [t_type_dossier] ([type_dossier],[intitule_type],[SENS],[soc_type_dossier]) VALUES ('I', N'Import', 'I', 'S01');
IF NOT EXISTS (SELECT 1 FROM [t_type_dossier] WHERE [type_dossier] = 'E')
    INSERT INTO [t_type_dossier] ([type_dossier],[intitule_type],[SENS],[soc_type_dossier]) VALUES ('E', N'Export', 'E', 'S01');

IF NOT EXISTS (SELECT 1 FROM [t_port] WHERE [code_port] = 'MAPTM')
    INSERT INTO [t_port] ([code_port],[libelle_port],[ville_port]) VALUES ('MAPTM', N'TANGER MED', N'Tanger');
IF NOT EXISTS (SELECT 1 FROM [t_port] WHERE [code_port] = 'MACAS')
    INSERT INTO [t_port] ([code_port],[libelle_port],[ville_port]) VALUES ('MACAS', N'CASABLANCA', N'Casablanca');

IF NOT EXISTS (SELECT 1 FROM [t_incoterm] WHERE [INCOTERM] = 'FOB')
    INSERT INTO [t_incoterm] ([INCOTERM],[LIBELLE_INCOTERM],[supp_incoterm],[facture_assigne_import],[facture_assigne_export],[livraison])
    VALUES ('FOB', N'Free On Board', 0, 0, 0, 0);
IF NOT EXISTS (SELECT 1 FROM [t_incoterm] WHERE [INCOTERM] = 'CIF')
    INSERT INTO [t_incoterm] ([INCOTERM],[LIBELLE_INCOTERM],[supp_incoterm],[facture_assigne_import],[facture_assigne_export],[livraison])
    VALUES ('CIF', N'Cost Insurance Freight', 0, 0, 0, 0);
IF NOT EXISTS (SELECT 1 FROM [t_incoterm] WHERE [INCOTERM] = 'EXW')
    INSERT INTO [t_incoterm] ([INCOTERM],[LIBELLE_INCOTERM],[supp_incoterm],[facture_assigne_import],[facture_assigne_export],[livraison])
    VALUES ('EXW', N'Ex Works', 0, 0, 0, 0);

-- Le code '10' signifie "pas de COD" : c'est une regle du code, pas une convention de nommage.
IF NOT EXISTS (SELECT 1 FROM [t_cod] WHERE [intitule_cod] = N'Pas de COD')
    INSERT INTO [t_cod] ([intitule_cod],[cod_edi]) VALUES (N'Pas de COD', '10');
IF NOT EXISTS (SELECT 1 FROM [t_cod] WHERE [intitule_cod] = N'COD a l''arrivee')
    INSERT INTO [t_cod] ([intitule_cod],[cod_edi]) VALUES (N'COD a l''arrivee', '20');

IF NOT EXISTS (SELECT 1 FROM [t_classe_client] WHERE [classe_client] = 'A' AND [societe] = 'S01')
    INSERT INTO [t_classe_client] ([classe_client],[societe]) VALUES ('A', 'S01');
IF NOT EXISTS (SELECT 1 FROM [t_classe_client] WHERE [classe_client] = 'B' AND [societe] = 'S01')
    INSERT INTO [t_classe_client] ([classe_client],[societe]) VALUES ('B', 'S01');

-- ---------------------------------------------------------------- clients
IF NOT EXISTS (SELECT 1 FROM [t_clients] WHERE [KEY_CLIENT] = 'CLI001')
    INSERT INTO [t_clients] ([KEY_CLIENT],[SOC_CLIENT],[RAISON_SOCIALE],[REF_CLIENT],[CLASSE_CLIENT],
        [ADRESSE1_CLIENT],[VILLE_CLIENT],[PAYS_CLIENT],[TEL_CLIENT],
        [FIN_MOIS],[RELEVE_FACTURE],[RAS],[VALIDE],[ACTIF],[ATTACHEMENT],[SUPPRIMER],[SUIVI_AT])
    VALUES ('CLI001','S01',N'ALPHA IMPORT SARL','REF001','A',
        N'12 rue des Douanes',N'Casablanca','MA',N'+212 522 000 001',
        0,0,0,1,1,0,0,0);

IF NOT EXISTS (SELECT 1 FROM [t_clients] WHERE [KEY_CLIENT] = 'CLI002')
    INSERT INTO [t_clients] ([KEY_CLIENT],[SOC_CLIENT],[RAISON_SOCIALE],[REF_CLIENT],[CLASSE_CLIENT],
        [ADRESSE1_CLIENT],[VILLE_CLIENT],[PAYS_CLIENT],[TEL_CLIENT],
        [FIN_MOIS],[RELEVE_FACTURE],[RAS],[VALIDE],[ACTIF],[ATTACHEMENT],[SUPPRIMER],[SUIVI_AT])
    VALUES ('CLI002','S01',N'BETA EXPORT SA','REF002','B',
        N'45 avenue du Port',N'Tanger','MA',N'+212 539 000 002',
        1,1,0,1,1,0,0,0);

-- ---------------------------------------------------------------- personnel (comptes de connexion)
IF NOT EXISTS (SELECT 1 FROM [t_personnel] WHERE [key_personnel] = 'P001')
    INSERT INTO [t_personnel] ([key_personnel],[soc_personnel],[agence_personnel],[nom_personnel],[prenom_personnel],
        [email_personnel],[identifiant_app],[pass_word],[bloque],[admin],[acces_web],[premiere_connexion],[attachement])
    VALUES ('P001','S01','AG01',N'BENALI',N'Karim',N'admin@transmar.test',N'admin',N'{admin}',0,1,1,0,0);

IF NOT EXISTS (SELECT 1 FROM [t_personnel] WHERE [key_personnel] = 'P002')
    INSERT INTO [t_personnel] ([key_personnel],[soc_personnel],[agence_personnel],[nom_personnel],[prenom_personnel],
        [email_personnel],[identifiant_app],[pass_word],[bloque],[admin],[acces_web],[premiere_connexion],[attachement])
    VALUES ('P002','S01','AG01',N'DUPONT',N'Marie',N'dupont@transmar.test',N'dupont',N'{utilisateur}',0,0,1,0,0);

COMMIT TRANSACTION;

-- ---------------------------------------------------------------- verification
SELECT 'societe'   AS [table], COUNT(*) AS [lignes] FROM [t_societe]
UNION ALL SELECT 'agence',    COUNT(*) FROM [t_agence]
UNION ALL SELECT 'devise',    COUNT(*) FROM [t_devise]
UNION ALL SELECT 'pays',      COUNT(*) FROM [T_PAYS]
UNION ALL SELECT 'clients',   COUNT(*) FROM [t_clients]
UNION ALL SELECT 'personnel', COUNT(*) FROM [t_personnel];
""";
    }
}
