using System.Globalization;
using System.Text;
using System.Text.Json;
using Logistics.Application.AI.RAG;

namespace Logistics.Application.AI.Agent;

/// <summary>Turns verified evidence into readable chat text. Never generates or infers business values.</summary>
internal static class AgentResponseFormatter
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
    private sealed record Field(string Name, string Label, bool Date = false, bool Money = false);

    public static string Business(string tool, JsonElement data)
    {
        if (tool == "GetTracking") return Tracking(data);
        if (data.ValueKind != JsonValueKind.Object) return "La consultation n’a pas fourni de fiche exploitable.\n\n";
        var (kind, titleField, source) = tool switch
        {
            "GetClient" => ("du client", "raisonSociale", "fiche client"),
            "GetInvoice" => ("de la facture", "numFacture", "facture"),
            "GetShipment" => ("du dossier d’expédition", "numDossier", "dossier d’expédition"),
            "GetBooking" => ("de la réservation", "numBooking", "réservation"),
            _ => ("de la fiche", "", "fiche")
        };
        var title = Text(data, titleField);
        var body = new StringBuilder();
        Fields(body, data, tool switch
        {
            "GetClient" => ClientFields,
            "GetInvoice" => InvoiceFields,
            "GetShipment" => ShipmentFields,
            "GetBooking" => BookingFields,
            _ => []
        });
        if (tool == "GetClient")
        {
            Flag(body, data, "actif", "Situation", "Actif", "Inactif");
            Flag(body, data, "valide", "Validation", "Validé", "Non validé");
            Rows(body, data, "adresses", "Adresses", AddressFields);
            Rows(body, data, "banques", "Coordonnées bancaires", BankFields);
        }
        if (tool == "GetInvoice")
        {
            Flag(body, data, "factRegle", "Règlement", "Réglée", "Non réglée");
            Rows(body, data, "lignes", "Prestations facturées", InvoiceLineFields);
            Rows(body, data, "tva", "Récapitulatif de TVA", [new("codeTva", "Code TVA"), new("ht", "Base HT", Money: true), new("tva", "TVA", Money: true)]);
        }
        if (tool is "GetShipment" or "GetBooking")
        {
            Flag(body, data, "marchandiseDangereuse", "Marchandise dangereuse", "Oui", "Non");
            Flag(body, data, "marchandiseFragile", "Marchandise fragile", "Oui", "Non");
            if (tool == "GetBooking") Flag(body, data, "depote", "Dépotage", "Effectué", "Non effectué");
            Rows(body, data, "colisages", "Colisages", PackageFields);
            if (tool == "GetBooking") Rows(body, data, "equipements", "Équipements", [new("typeEquipement", "Type"), new("natureEquipement", "Nature"), new("qte", "Quantité")]);
        }
        if (body.Length == 0) return "La consultation a réussi, mais aucune information renseignée n’a été fournie pour cette fiche.\n\n";
        return $"Voici les informations {kind}{(title is null ? "" : " « " + title + " »")} :\n\n{body}\nSource : {source} consultée dans l’application.\n\n";
    }

    public static string Failure(string tool, string code)
    {
        var entity = tool switch
        {
            "GetClient" => "ce client", "GetInvoice" => "cette facture", "GetShipment" => "ce dossier d’expédition",
            "GetBooking" => "cette réservation", "GetTracking" => "ce suivi", _ => "cette fiche"
        };
        return (code switch
        {
            "identifier_required" => $"Pour retrouver {entity}, indiquez sa clé interne ou sa référence exacte.",
            "not_found" => $"Je n’ai trouvé aucun résultat pour {entity} avec la référence fournie. Vérifiez la référence ou la clé interne.",
            "forbidden" => $"Vous n’avez pas l’autorisation de consulter {entity}.",
            "validation_failed" => $"Je n’ai pas pu consulter {entity}. Vérifiez la référence fournie.",
            "tool_not_allowed" => "Cette action n’est pas disponible. Je peux consulter les clients, factures, expéditions, suivis et réservations.",
            "duplicate_call" => "Cette demande de consultation a déjà été traitée.",
            "invalid_tool_result" => $"La réponse reçue pour {entity} est incomplète. Je ne peux pas en confirmer les informations.",
            "business_rule_violation" => $"La consultation de {entity} a été refusée. Vérifiez la référence ; si elle correspond à plusieurs fiches, utilisez la clé interne.",
            _ => $"Je ne peux pas consulter {entity} pour le moment : le service de données est indisponible. Réessayez dans un instant."
        }) + "\n\n";
    }

    public static string Document(RetrievedPassage passage) =>
        $"Voici un extrait pertinent de vos documents :\n\n{passage.Chunk.Text}\n\nSource documentaire : {SingleLine(passage.Chunk.SourceName ?? passage.Chunk.DocumentId)}, passage {passage.Chunk.Index + 1}.\n\n";

    private static string Tracking(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Array) return "Le suivi reçu n’est pas exploitable.\n\n";
        if (data.GetArrayLength() == 0) return "Aucun événement de suivi n’est disponible pour cette référence.\n\n";
        var text = new StringBuilder("Voici les événements de suivi disponibles, du plus récent au plus ancien :\n");
        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            text.AppendLine();
            Fields(text, item, [new("cleEntite", "Référence suivie"), new("entite", "Élément suivi"),
                new("libelleStatut", "Statut"), new("keyStatut", "Code du statut"),
                new("dateTraceUser", "Date de l’événement", Date: true), new("utilisateur", "Auteur"), new("commentaire", "Commentaire")]);
        }
        text.AppendLine("\nSource : événements de suivi consultés dans l’application.\n");
        return text.ToString();
    }

    private static void Fields(StringBuilder text, JsonElement data, Field[] fields)
    {
        foreach (var field in fields)
        {
            if (!data.TryGetProperty(field.Name, out var value)) continue;
            string? formatted = value.ValueKind switch
            {
                JsonValueKind.String => Text(data, field.Name),
                JsonValueKind.Number when value.TryGetDecimal(out var number) => number.ToString(field.Money ? "0.00##########################" : "0.############################", French),
                _ => null
            };
            if (formatted is null) continue;
            if (field.Date)
            {
                if (value.ValueKind != JsonValueKind.String || !value.TryGetDateTimeOffset(out var date) || date.Year == 1) continue;
                var original = value.GetString()!;
                var explicitOffset = original.Length >= 6 && original[^3] == ':' && original[^6] is '+' or '-';
                formatted = date.ToString(date.TimeOfDay == TimeSpan.Zero ? "dd/MM/yyyy" : "dd/MM/yyyy à HH:mm", French)
                    + (original.EndsWith('Z') ? " UTC" : explicitOffset ? " UTC" + original[^6..] : "");
            }
            text.Append("• ").Append(field.Label).Append(" : ").AppendLine(formatted);
        }
    }

    private static void Flag(StringBuilder text, JsonElement data, string field, string label, string yes, string no)
    {
        if (data.TryGetProperty(field, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            text.Append("• ").Append(label).Append(" : ").AppendLine(value.GetBoolean() ? yes : no);
    }

    private static void Rows(StringBuilder text, JsonElement data, string property, string title, Field[] fields)
    {
        if (!data.TryGetProperty(property, out var rows) || rows.ValueKind != JsonValueKind.Array) return;
        var items = new StringBuilder();
        var index = 0;
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object) continue;
            var lines = new StringBuilder(); Fields(lines, row, fields);
            if (lines.Length == 0) continue;
            items.Append(++index).AppendLine(".").Append(lines);
        }
        if (items.Length > 0) text.AppendLine().Append(title).AppendLine(" :").Append(items);
    }

    private static string? Text(JsonElement data, string property) => property.Length > 0
        && data.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(value.GetString()) ? SingleLine(value.GetString()!) : null;
    private static string SingleLine(string value) => string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static readonly Field[] ClientFields = [new("raisonSociale", "Nom du client"), new("refClient", "Référence client"), new("keyClient", "Identifiant"),
        new("adresse1Client", "Adresse"), new("adresse2Client", "Complément d’adresse"), new("cpClient", "Code postal"), new("villeClient", "Ville"), new("paysClient", "Pays (code)"),
        new("telClient", "Téléphone"), new("faxClient", "Fax"), new("siteWebClient", "Site web"), new("statutClient", "Statut enregistré"), new("classeClient", "Classe client"),
        new("activiteClient", "Activité"), new("observationClient", "Observation"), new("iceClient", "ICE"), new("ifClient", "Identifiant fiscal"), new("rcClient", "Registre de commerce"),
        new("deviseFacturation", "Devise de facturation"), new("paiementClient", "Mode de paiement"), new("plafond", "Plafond déclaré", Money: true)];
    private static readonly Field[] AddressFields = [new("adresse1", "Adresse"), new("adresse2", "Complément"), new("cp", "Code postal"), new("ville", "Ville"), new("pays", "Pays (code)"), new("contact", "Contact"), new("telAdresse", "Téléphone")];
    private static readonly Field[] BankFields = [new("banque", "Banque"), new("compteRib", "RIB"), new("swift", "SWIFT"), new("ibanCode", "IBAN")];
    private static readonly Field[] InvoiceFields = [new("numFacture", "Numéro"), new("keyFacture", "Identifiant"), new("typeFacture", "Type"), new("intituleClient", "Client"),
        new("dateFacture", "Date de facture", Date: true), new("dateEcheance", "Échéance", Date: true), new("deviseFacture", "Devise"),
        new("totalHtMad", "Total HT (MAD)", Money: true), new("tvaMad", "TVA (MAD)", Money: true), new("ttcMad", "Total TTC (MAD)", Money: true),
        new("ttcDevise", "Total TTC dans la devise de facture", Money: true), new("mtRegle", "Montant réglé (MAD)", Money: true), new("encoursMad", "Solde (MAD)", Money: true), new("netAPayer", "Net à payer (MAD)", Money: true)];
    private static readonly Field[] InvoiceLineFields = [new("libelleRubrique", "Prestation"), new("mtTotalDevise", "Montant brut", Money: true), new("mtDevise", "Montant net", Money: true), new("mtHtMad", "HT (MAD)", Money: true), new("codeTva", "Code TVA")];
    private static readonly Field[] ShipmentFields = [new("numDossier", "Numéro de dossier"), new("keyDossier", "Identifiant"), new("intituleClient", "Client"), new("typeDossier", "Type de dossier"),
        new("libelleStatutDossier", "Statut"), new("keyStatutDossier", "Code du statut"), new("statutFacturation", "Facturation"), new("statutCloture", "Clôture"), new("descriptionMarchandise", "Marchandise"),
        new("intituleExpediteur", "Expéditeur"), new("intituleDestinataire", "Destinataire"), new("incoterm", "Incoterm"), new("nbColis", "Nombre de colis"), new("poidsBrut", "Poids brut"), new("volume", "Volume"),
        new("dateCreation", "Création", Date: true), new("dateDepotage", "Date de dépotage", Date: true)];
    private static readonly Field[] BookingFields = [new("numBooking", "Numéro de réservation"), new("keyBooking", "Identifiant"), new("intituleClient", "Client"), new("typeBooking", "Type"),
        new("keyStatutBooking", "Statut enregistré"), new("commentaireStatutBooking", "Commentaire du statut"), new("descriptionMarchandise", "Marchandise"), new("intituleExpediteur", "Expéditeur"), new("intituleDestinataire", "Destinataire"),
        new("portDepart", "Port de départ"), new("incoterm", "Incoterm"), new("nbTotalColis", "Nombre de colis"), new("poidsBrutPrevision", "Poids brut prévu"), new("volumePrevision", "Volume prévu"), new("nbEquipements", "Nombre d’équipements"),
        new("dateCreation", "Création", Date: true), new("dateValidite", "Validité", Date: true), new("dateDepartPrevue", "Départ prévu", Date: true), new("commentaire", "Commentaire")];
    private static readonly Field[] PackageFields = [new("typeColis", "Type de colis"), new("natureMarchandise", "Nature"), new("nbColis", "Nombre de colis"), new("qte", "Quantité"), new("poidsBrut", "Poids brut"), new("poids", "Poids"), new("volume", "Volume")];
}
