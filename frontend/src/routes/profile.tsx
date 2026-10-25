import { createFileRoute } from "@tanstack/react-router";
import { LoadingBlock } from "@/components/common/bits";
import { ProfileView } from "@/components/profile/ProfileView";
import { useRequireAuth } from "@/lib/state/auth";

export const Route = createFileRoute("/profile")({
  head: () => ({ meta: [{ title: "Profile — Echelon Chess" }] }),
  component: Profile,
});

function Profile() {
  const { user } = useRequireAuth();
  if (!user) return <LoadingBlock className="py-32" />;
  return <ProfileView username={user.username} />;
}
