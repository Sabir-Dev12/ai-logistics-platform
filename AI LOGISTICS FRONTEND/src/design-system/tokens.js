import values from "./tokens.json";

function freeze(value) {
  Object.values(value).forEach((child) => {
    if (child && typeof child === "object") freeze(child);
  });
  return Object.freeze(value);
}

export const tokens = freeze(values);
