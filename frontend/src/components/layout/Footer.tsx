const Footer = () => {
  return (
    <footer style={{
      padding: '1.5rem 2rem',
      borderTop: '1px solid var(--color-border)',
      backgroundColor: 'var(--color-bg)',
      textAlign: 'center',
      marginTop: 'auto',
    }}>
      <p style={{ margin: 0, color: 'var(--color-text-3)', fontSize: '0.85rem' }}>
        QuotationHub &copy; {new Date().getFullYear()} | A community-driven quotation collection
      </p>
    </footer>
  );
};

export default Footer;
