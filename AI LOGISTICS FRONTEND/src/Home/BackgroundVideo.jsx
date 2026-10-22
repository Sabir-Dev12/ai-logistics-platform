import { useEffect, useRef, useState } from "react";
import PropTypes from "prop-types";
import { heroPoster, heroVideoSource } from "./mediaAssets";

export default function BackgroundVideo({ playing = true }) {
  const videoRef = useRef(null);
  const [ready, setReady] = useState(false);
  useEffect(() => {
    const video = videoRef.current;
    if (!video || !playing) return;
    let disposed = false, hls = null, recovered = false;
    video.muted = true;
    const play = () => {
      if (disposed || document.hidden) return;
      video.play()?.catch(() => { if (!disposed) setReady(false); });
    };
    const fallback = () => { hls?.destroy(); hls = null; if (!disposed) setReady(false); };
    const visibility = () => {
      if (document.hidden) { video.pause(); hls?.stopLoad(); }
      else { hls?.startLoad(); play(); }
    };
    document.addEventListener("visibilitychange", visibility);
    const initialize = async () => {
      try {
        const { default: Hls } = await import("hls.js");
        if (disposed) return;
        if (Hls.isSupported()) {
          hls = new Hls({ capLevelToPlayerSize: true, maxBufferLength: 15, maxMaxBufferLength: 30 });
          hls.on(Hls.Events.MANIFEST_PARSED, play);
          hls.on(Hls.Events.ERROR, (_, data) => {
            if (!data.fatal || disposed) return;
            if (data.type === Hls.ErrorTypes.MEDIA_ERROR && !recovered) { recovered = true; hls.recoverMediaError(); }
            else fallback();
          });
          hls.loadSource(heroVideoSource); hls.attachMedia(video);
        } else if (video.canPlayType("application/vnd.apple.mpegurl")) {
          video.addEventListener("loadedmetadata", play); video.src = heroVideoSource;
        }
      } catch { fallback(); }
    };
    void initialize();
    return () => {
      disposed = true; document.removeEventListener("visibilitychange", visibility);
      video.removeEventListener("loadedmetadata", play); hls?.destroy();
      video.pause(); video.removeAttribute("src"); video.load();
    };
  }, [playing]);
  return <div className="hero-media" data-ready={ready && playing ? "true" : "false"} aria-hidden="true">
    <img className="hero-poster" src={heroPoster} alt="" loading="eager" decoding="async" />
    <video ref={videoRef} className="hero-video" muted loop playsInline autoPlay={playing} preload="metadata" poster={heroPoster} tabIndex={-1}
      onPlaying={() => setReady(true)} onError={() => setReady(false)} />
  </div>;
}
BackgroundVideo.propTypes = { playing: PropTypes.bool };
