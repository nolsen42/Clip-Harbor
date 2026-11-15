// On X/Twitter, remembers which post was right-clicked, so "Download with ClipHarbor"
// sends that post even from the timeline (where the page address is just the home feed).

(() => {
  const api = globalThis.browser ?? globalThis.chrome;
  let lastPost = null;

  function postLink(target) {
    const article = target?.closest?.("article");
    if (!article) return null;
    // The post's own link is the one around its time stamp.
    const a = article.querySelector('a[href*="/status/"] time')?.closest("a") ?? article.querySelector('a[href*="/status/"]');
    if (!a) return null;
    const url = new URL(a.getAttribute("href"), location.origin);
    url.pathname = url.pathname.replace(/\/(photo|video|analytics|media_tags|quotes|retweets|likes)(\/.*)?$/, "");
    url.search = ""; url.hash = "";
    return url.href;
  }

  document.addEventListener("contextmenu", (e) => { lastPost = postLink(e.target); }, true);

  api.runtime.onMessage.addListener((message, _sender, reply) => {
    if (message?.type === "clipharbor-target") reply({ url: lastPost });
  });
})();
