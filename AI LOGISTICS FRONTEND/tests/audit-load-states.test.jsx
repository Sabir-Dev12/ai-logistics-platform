import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import BookingsPage from "../src/Bookings/BookingsPage";
import ShipmentsPage from "../src/Shipments/ShipmentsPage";
describe("Audit regression: unavailable lists",()=>{
 it.each([[BookingsPage,"Aucune réservation","Créer une réservation"],[ShipmentsPage,"Aucun dossier","Créer un dossier"]])("does not turn a failed read into a first-record invitation",async(Page,empty,create)=>{
  const repository={serverPaging:true,list:async()=>{throw new Error("Échec de lecture confirmé");},types:async()=>[],references:async()=>({items:[],totalCount:0})};
  render(<Page repository={repository}/>);await screen.findByText("Échec de lecture confirmé");expect(screen.queryByText(empty)).toBeNull();expect(screen.getByRole("button",{name:create,exact:true}).disabled).toBe(true);
 });
});
