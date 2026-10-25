import { createFileRoute } from "@tanstack/react-router";
import { ProfileView } from "@/components/profile/ProfileView";

export const Route = createFileRoute("/players/$username")({
  head: ({ params }) => ({ meta: [{ title: `${params.username} — Echelon Chess` }] }),
  component: Player,
});

function Player() {
  const { username } = Route.useParams();
  return <ProfileView key={username} username={username} />;
}
