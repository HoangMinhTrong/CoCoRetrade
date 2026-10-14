document.querySelectorAll('input[data-preview]').forEach(input => {
  input.addEventListener('change', () => {
    const target = document.getElementById(input.dataset.preview);
    target.replaceChildren();
    for (const file of Array.from(input.files).slice(0, 10)) {
      if (!file.type.startsWith('image/')) continue;
      const image = document.createElement('img');
      image.alt = file.name;
      image.src = URL.createObjectURL(file);
      image.onload = () => URL.revokeObjectURL(image.src);
      target.append(image);
    }
  });
});
