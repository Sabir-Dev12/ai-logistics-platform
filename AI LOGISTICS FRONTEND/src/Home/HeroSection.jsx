import { useState } from "react";
import PropTypes from "prop-types";
import { motion, useReducedMotion } from "motion/react";
import { Pause, Play } from "lucide-react";
import BackgroundVideo from "./BackgroundVideo";

export default function HeroSection({ children }) {
  const reducedMotion = useReducedMotion();
  const [paused, setPaused] = useState(() => Boolean(navigator.connection?.saveData));
  const transition = (duration, delay = 0) => ({ duration: reducedMotion ? 0 : duration, delay: reducedMotion ? 0 : delay });
  return <main className="home-hero" id="home-content" tabIndex={-1}><BackgroundVideo playing={!paused && !reducedMotion} />
    <div className="hero-overlay" aria-hidden="true" /><div className="hero-glow hero-glow-blue" aria-hidden="true" /><div className="hero-glow hero-glow-indigo" aria-hidden="true" />
    <motion.div className="hero-content" initial={reducedMotion ? false : { opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={transition(0.6)}>{children}</motion.div>
    {!reducedMotion && <button className="hero-video-toggle" aria-label={paused ? "Play background video" : "Pause background video"} aria-pressed={paused} onClick={() => setPaused(!paused)}>
      {paused ? <Play size={14} aria-hidden="true" /> : <Pause size={14} aria-hidden="true" />}
    </button>}
  </main>;
}
HeroSection.propTypes = { children: PropTypes.node.isRequired };
