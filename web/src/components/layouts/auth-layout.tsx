interface AuthLayoutProps {
  children: React.ReactNode;
}

export function AuthLayout({ children }: AuthLayoutProps) {
  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50 px-4">
      <div className="w-full max-w-md">
        <div className="text-center mb-8">
          <h1 className="text-4xl font-bold text-gray-900">IEP Assistant</h1>
          <p className="text-gray-500 mt-2">Understand and advocate for your child's IEP</p>
        </div>
        {children}
      </div>
    </div>
  );
}
