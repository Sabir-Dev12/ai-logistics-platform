import PropTypes from "prop-types";
import DashboardPage from "../Dashboard/DashboardPage";
import HomeNavbar from "./HomeNavbar";
import HeroSection from "./HeroSection";
import "./home.css";

export default function HomePage({ navigation, displayName, onLogout, repository }) {
  return <div className="home-page"><a className="home-skip" href="#home-content">Aller au contenu principal</a>
    <HomeNavbar navigation={navigation} displayName={displayName} onLogout={onLogout} />
    <HeroSection><div className="home-dashboard app-main-content"><DashboardPage repository={repository} /></div></HeroSection>
  </div>;
}
HomePage.propTypes = {
  navigation: PropTypes.array.isRequired,
  displayName: PropTypes.string,
  onLogout: PropTypes.func.isRequired,
  repository: DashboardPage.propTypes.repository,
};
