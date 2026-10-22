import { useMemo } from "react";
import { useAuth } from "../Auth/useAuth";
import { createBookingsService } from "../services/bookingsService";
import BookingsPage from "./BookingsPage";
export default function ConnectedBookingsPage() {
 const {request}=useAuth();
 const repository=useMemo(()=>createBookingsService({request}),[request]);
 return <BookingsPage repository={repository} />;
}
