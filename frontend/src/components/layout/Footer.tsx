const Footer = () => {
  return (
    <footer
      style={{
        padding: '2rem',
        backgroundColor: '#f8f9fa',
        borderTop: '1px solid #dee2e6',
        textAlign: 'center',
        marginTop: 'auto',
      }}
    >
      <p style={{ margin: 0, color: '#6c757d' }}>
        Quotations &copy; {new Date().getFullYear()} | A community-driven quotation management system
      </p>
      <p style={{ margin: '0.5rem 0 0 0', fontSize: '0.875rem', color: '#6c757d' }}>
        Built with React, TypeScript, ASP.NET Core, and MongoDB
      </p>
    </footer>
  );
};

export default Footer;
