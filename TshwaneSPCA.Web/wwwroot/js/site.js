const navigation = document.querySelector("#mainNav");
const mobileToggle = document.querySelector("#mobileToggle");
const disclosures = [...navigation.querySelectorAll(".dropdown > button")];

function setDisclosure(button, open) {
  button.parentElement.classList.toggle("open", open);
  button.setAttribute("aria-expanded", String(open));
}

function closeDisclosures() {
  disclosures.forEach((button) => setDisclosure(button, false));
}

function setMobileMenu(open) {
  navigation.classList.toggle("show", open);
  mobileToggle.setAttribute("aria-expanded", String(open));
  mobileToggle.textContent = open ? "×" : "☰";
  if (!open) closeDisclosures();
}

mobileToggle.addEventListener("click", () => {
  setMobileMenu(!navigation.classList.contains("show"));
});

disclosures.forEach((button) => {
  button.addEventListener("click", () => {
    const wasOpen = button.getAttribute("aria-expanded") === "true";
    closeDisclosures();
    if (!wasOpen) setDisclosure(button, true);
  });
});

navigation.addEventListener("click", (event) => {
  const link = event.target.closest("a");
  if (!link) return;
  setMobileMenu(false);

  // Move focus to the destination before the navigation link is hidden.
  if (link.hash) {
    const target = document.getElementById(link.hash.slice(1));
    if (target) {
      target.tabIndex = -1;
      target.focus({ preventScroll: true });
    }
  }
});

document.addEventListener("pointerdown", (event) => {
  if (
    !navigation.contains(event.target) &&
    !mobileToggle.contains(event.target)
  ) {
    setMobileMenu(false);
  }
});

document.addEventListener("keydown", (event) => {
  if (event.key !== "Escape" || document.querySelector("#lightbox").open)
    return;
  const openDisclosure = disclosures.find(
    (button) => button.getAttribute("aria-expanded") === "true",
  );
  if (openDisclosure) {
    setDisclosure(openDisclosure, false);
    openDisclosure.focus();
  } else if (navigation.classList.contains("show")) {
    setMobileMenu(false);
    mobileToggle.focus();
  }
});

const posterDialog = document.querySelector("#lightbox");
const posterImage = document.querySelector("#lightboxImage");

document.querySelectorAll("[data-view]").forEach((button) => {
  button.addEventListener("click", () => {
    const image = button.closest(".poster-card").querySelector("img");
    posterImage.src = image.currentSrc || image.src;
    posterImage.alt = image.alt;
    posterDialog.showModal();
  });
});

document.querySelector("#closeLightbox").addEventListener("click", () => {
  posterDialog.close();
});
posterDialog.addEventListener("click", (event) => {
  if (event.target === posterDialog) posterDialog.close();
});
posterDialog.addEventListener("close", () =>
  posterImage.removeAttribute("src"),
);

const copyButton = document.querySelector("[data-copy]");
let copyReset;
copyButton?.addEventListener("click", async () => {
  clearTimeout(copyReset);
  try {
    await navigator.clipboard.writeText(copyButton.dataset.copy);
    copyButton.textContent = "Copied ✓";
  } catch {
    copyButton.textContent = "Select number to copy";
  }
  copyReset = setTimeout(() => (copyButton.textContent = "Copy"), 2000);
});
