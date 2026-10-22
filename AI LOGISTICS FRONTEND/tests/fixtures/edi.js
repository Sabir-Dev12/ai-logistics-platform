export const analysisDto=(o={})=>({fichierAccepte:true,nbLignes:2,erreursFichier:[],entetes:[{numeroLigne:1,numVoyage:"EDI0001",portDepart:"CASABLANCA",portArrive:"TANGER",dateDepart:"2026-10-17T00:00:00",dateArrivee:"2026-10-18T00:00:00",nbDossiers:1,anomalies:[]}],dossiers:[{numeroLigne:2,numVoyage:"EDI0001",position:1,numDossier:"EDI0001-1",codeDestinataire:"CLIENT01",colis:10,poids:1000,volume:10,incoterm:"DA",devise:"EUR",anomalies:[]}],nbAnomalies:0,...o});
export const stagedDto=(o={})=>({fichierAccepte:true,nbLignes:2,nbEntetes:1,nbDossiers:1,nbAnomalies:0,erreursFichier:[],anomalies:[],...o});
export const reportDto=(o={})=>({promouvable:true,nbVoyages:1,nbDossiers:1,anomalies:[],...o});
export const promotionDto=(o={})=>({nbVoyagesCrees:1,nbDossiersCrees:1,referencesVoyages:["EDI0001"],...o});
export const selection=()=>({file:new File(["x".repeat(1000)+"\r\n"+"y".repeat(1000)],"import.stc",{type:"text/plain"}),selectionId:1});
